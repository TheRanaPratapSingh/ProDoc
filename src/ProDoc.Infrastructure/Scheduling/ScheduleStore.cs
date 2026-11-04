using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Common;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Scheduling;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Scheduling;

public sealed class ScheduleStore(ProDocDbContext db) : IScheduleStore
{
    public Task<ConsultationSchedule?> FindAsync(Guid scheduleId, bool tracking, CancellationToken cancellationToken) =>
        Schedules(tracking).SingleOrDefaultAsync(schedule => schedule.Id == scheduleId, cancellationToken);

    public async Task<IReadOnlyList<ConsultationSchedule>> ListForDoctorAsync(Guid doctorId, CancellationToken cancellationToken) =>
        await Schedules(tracking: false).Where(schedule => schedule.DoctorId == doctorId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ConsultationSchedule>> ListForClinicsAsync(
        IReadOnlyCollection<Guid> clinicIds,
        CancellationToken cancellationToken) =>
        await Schedules(tracking: false).Where(schedule => schedule.ClinicId != null && clinicIds.Contains(schedule.ClinicId.Value)).ToListAsync(cancellationToken);

    public Task AddAsync(ConsultationSchedule schedule, CancellationToken cancellationToken)
    {
        db.ConsultationSchedules.Add(schedule);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid doctorId, Guid? clinicId, Guid? branchId, CancellationToken cancellationToken) =>
        db.ConsultationSchedules.AnyAsync(
            schedule => schedule.DoctorId == doctorId && schedule.ClinicId == clinicId && schedule.BranchId == branchId,
            cancellationToken);

    public async Task<IReadOnlyList<ConsultationSchedule>> ListPublishedForDoctorAsync(Guid doctorId, CancellationToken cancellationToken) =>
        await Schedules(tracking: false).Where(schedule => schedule.DoctorId == doctorId && schedule.IsPublished).ToListAsync(cancellationToken);

    public void Remove(ConsultationSchedule schedule) => db.ConsultationSchedules.Remove(schedule);

    public async Task<IReadOnlyList<DiscoveryPlace>> SearchAsync(
        DiscoveryQuery query,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var matches = await Places(today, query, doctorId: null).Take(200).ToListAsync(cancellationToken);

        IEnumerable<DiscoveryPlace> places = matches.Select(row => row.ToPlace(query.Latitude, query.Longitude));
        if (query.Latitude is double originLat && query.Longitude is double originLng && query.RadiusKm is double radiusKm)
        {
            places = places.Where(place => place.DistanceKm is double distance && distance <= radiusKm);
        }

        var ordered = await OrderAsync(places.ToArray(), query, today, cancellationToken);
        var skip = (query.Page - 1) * query.PageSize;
        return ordered.Skip(skip).Take(query.PageSize).ToArray();
    }

    public async Task<DiscoveredDoctor?> FindDiscoveredDoctorAsync(Guid doctorId, DateOnly today, CancellationToken cancellationToken)
    {
        var doctor = await db.Doctors.AsNoTracking()
            .Include(item => item.Qualifications)
            .Include(item => item.Specializations)
            .Include(item => item.Languages)
            .Include(item => item.Registrations)
            .Include(item => item.Documents)
            .Include(item => item.Expertise)
            .SingleOrDefaultAsync(item => item.Id == doctorId, cancellationToken);
        if (doctor is null || !doctor.IsPubliclyBookable(today))
        {
            return null;
        }

        var ownPlaces = await Places(today, EmptyQuery(), doctorId).ToListAsync(cancellationToken);
        if (ownPlaces.Count == 0)
        {
            return null;
        }

        var names = await db.Specializations.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
        var primary = doctor.Specializations.SingleOrDefault(link => link.IsPrimary);
        return new DiscoveredDoctor(
            doctor.Id,
            doctor.DisplayName ?? $"{doctor.FirstName} {doctor.LastName}".Trim(),
            doctor.Biography,
            doctor.YearsOfExperience ?? 0,
            doctor.Gender,
            primary is not null && names.TryGetValue(primary.SpecializationId, out var primaryName) ? primaryName.Name : string.Empty,
            doctor.Specializations
                .Select(link => names.TryGetValue(link.SpecializationId, out var item) ? item.Name : null)
                .Where(name => name is not null)
                .Cast<string>()
                .Order(StringComparer.Ordinal)
                .ToArray(),
            doctor.Qualifications.Select(item => item.Name).Order(StringComparer.Ordinal).ToArray(),
            doctor.Expertise.Select(item => item.Label).Order(StringComparer.Ordinal).ToArray(),
            doctor.Languages.Select(item => item.Code).Order(StringComparer.Ordinal).ToArray(),
            doctor.Registrations.SingleOrDefault(item => item.IsCurrent)?.Council,
            doctor.Registrations.SingleOrDefault(item => item.IsCurrent)?.State,
            doctor.Documents.SingleOrDefault(document => document.IsCurrent && document.Category == DocumentCategory.ProfilePhoto)?.Id,
            ownPlaces.Select(row => row.ToPlace(null, null)).ToArray());
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw DomainException.Conflict("concurrency_conflict", "This schedule was updated. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        {
            throw DomainException.Conflict("schedule_exists", "A schedule already exists for this place.");
        }
    }

    public async Task<IReadOnlyList<DiscoveredClinic>> SearchClinicsAsync(DiscoveryQuery query, DateOnly today, CancellationToken cancellationToken)
    {
        var clinics = await ClinicRows(query, today).Take(100).ToListAsync(cancellationToken);
        return await ProjectClinics(clinics, query, today, cancellationToken);
    }

    public async Task<DiscoveredClinic?> FindDiscoveredClinicAsync(Guid clinicId, DateOnly today, CancellationToken cancellationToken)
    {
        var clinic = await ListedClinics(today).Include(item => item.Branches).FirstOrDefaultAsync(item => item.Id == clinicId, cancellationToken);
        if (clinic is null)
        {
            return null;
        }

        var projected = await ProjectClinics([clinic], EmptyQuery(), today, cancellationToken);
        return projected.SingleOrDefault();
    }

    public async Task<IReadOnlyList<SearchSuggestion>> SuggestAsync(string text, CancellationToken cancellationToken)
    {
        var specializations = await db.Specializations.AsNoTracking()
            .Where(item => item.IsActive && item.Name.Contains(text))
            .OrderBy(item => item.Name)
            .Take(4)
            .Select(item => new SearchSuggestion("specialization", item.Name, item.Id))
            .ToListAsync(cancellationToken);
        var cities = await db.ClinicBranches.AsNoTracking()
            .Where(item => item.IsActive && item.City.Contains(text))
            .Select(item => item.City)
            .Distinct()
            .OrderBy(item => item)
            .Take(4)
            .ToListAsync(cancellationToken);
        var clinics = await db.Clinics.AsNoTracking()
            .Where(item => item.Status == VerificationStatus.Approved && item.Name != null && item.Name.Contains(text))
            .OrderBy(item => item.Name)
            .Take(4)
            .Select(item => new SearchSuggestion("clinic", item.Name!, item.Id))
            .ToListAsync(cancellationToken);
        return specializations
            .Concat(cities.Select(city => new SearchSuggestion("city", city, null)))
            .Concat(clinics)
            .Take(8)
            .ToArray();
    }

    public async Task<IReadOnlyList<BranchOperatingHour>> ListOperatingHoursAsync(Guid branchId, CancellationToken cancellationToken) =>
        await db.BranchOperatingHours.AsNoTracking().Where(item => item.BranchId == branchId).ToListAsync(cancellationToken);

    public async Task ReplaceOperatingHoursAsync(Guid branchId, IReadOnlyList<BranchOperatingHour> hours, CancellationToken cancellationToken)
    {
        var existing = await db.BranchOperatingHours.Where(item => item.BranchId == branchId).ToListAsync(cancellationToken);
        db.BranchOperatingHours.RemoveRange(existing);
        db.BranchOperatingHours.AddRange(hours);
    }

    public async Task<IReadOnlyList<BranchClosure>> ListBranchClosuresAsync(Guid branchId, CancellationToken cancellationToken) =>
        await db.BranchClosures.AsNoTracking().Where(item => item.BranchId == branchId).ToListAsync(cancellationToken);

    public Task AddBranchClosureAsync(BranchClosure closure, CancellationToken cancellationToken)
    {
        db.BranchClosures.Add(closure);
        return Task.CompletedTask;
    }

    public async Task<bool> RemoveBranchClosureAsync(Guid branchId, Guid closureId, CancellationToken cancellationToken)
    {
        var closure = await db.BranchClosures.SingleOrDefaultAsync(item => item.Id == closureId && item.BranchId == branchId, cancellationToken);
        if (closure is null)
        {
            return false;
        }

        db.BranchClosures.Remove(closure);
        return true;
    }

    public async Task ReplaceFacilitiesAsync(Guid clinicId, IReadOnlyList<ClinicFacility> facilities, CancellationToken cancellationToken)
    {
        var existing = await db.ClinicFacilities.Where(item => item.ClinicId == clinicId).ToListAsync(cancellationToken);
        db.ClinicFacilities.RemoveRange(existing);
        db.ClinicFacilities.AddRange(facilities);
    }

    public async Task<IReadOnlyList<Guid>> ListFavouriteDoctorIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.DoctorFavourites.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.DoctorId)
            .ToListAsync(cancellationToken);

    public Task AddFavouriteAsync(DoctorFavourite favourite, CancellationToken cancellationToken)
    {
        db.DoctorFavourites.Add(favourite);
        return Task.CompletedTask;
    }

    public async Task<bool> RemoveFavouriteAsync(Guid userId, Guid doctorId, CancellationToken cancellationToken)
    {
        var favourite = await db.DoctorFavourites.SingleOrDefaultAsync(item => item.UserId == userId && item.DoctorId == doctorId, cancellationToken);
        if (favourite is null)
        {
            return false;
        }

        db.DoctorFavourites.Remove(favourite);
        return true;
    }

    private async Task<IReadOnlyList<DiscoveryPlace>> OrderAsync(
        IReadOnlyList<DiscoveryPlace> places,
        DiscoveryQuery query,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (query.Sort == "earliest")
        {
            var ids = places.Select(place => place.ScheduleId).ToArray();
            var schedules = await Schedules(false).Where(schedule => ids.Contains(schedule.Id)).ToListAsync(cancellationToken);
            var rank = schedules.ToDictionary(schedule => schedule.Id, schedule => FirstSlot(schedule, today));
            return places.OrderBy(place => rank.GetValueOrDefault(place.ScheduleId, DateTime.MaxValue)).ToArray();
        }

        return query.Sort switch
        {
            "fee" => places.OrderBy(place => place.ConsultationFee).ThenBy(place => place.DisplayName).ToArray(),
            "experience" => places.OrderByDescending(place => place.YearsOfExperience).ThenBy(place => place.ConsultationFee).ToArray(),
            "nearest" => places.OrderBy(place => place.DistanceKm ?? double.MaxValue).ThenBy(place => place.ConsultationFee).ToArray(),
            _ when query.Latitude is not null => places.OrderBy(place => place.DistanceKm ?? double.MaxValue).ThenBy(place => place.ConsultationFee).ToArray(),
            _ => places.OrderByDescending(place => place.YearsOfExperience).ThenBy(place => place.ConsultationFee).ToArray()
        };
    }

    private static DateTime FirstSlot(ConsultationSchedule schedule, DateOnly today)
    {
        for (var offset = 0; offset <= 14; offset++)
        {
            var date = today.AddDays(offset);
            var slots = schedule.GenerateSlots(date);
            if (slots.Count > 0)
            {
                return date.ToDateTime(slots[0].Start);
            }
        }

        return DateTime.MaxValue;
    }

    private IQueryable<Clinic> ClinicRows(DiscoveryQuery query, DateOnly today)
    {
        var rows = ListedClinics(today);
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            rows = rows.Where(clinic =>
                (clinic.Name != null && clinic.Name.Contains(text))
                || clinic.Branches.Any(branch => branch.IsActive && (branch.City.Contains(text) || branch.PostalCode.Contains(text) || branch.AddressLine.Contains(text))));
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();
            rows = rows.Where(clinic => clinic.Branches.Any(branch => branch.IsActive && branch.City.Contains(city)));
        }

        if (!string.IsNullOrWhiteSpace(query.PostalCode))
        {
            var postalCode = query.PostalCode.Trim();
            rows = rows.Where(clinic => clinic.Branches.Any(branch => branch.IsActive && branch.PostalCode == postalCode));
        }

        if (!string.IsNullOrWhiteSpace(query.Locality))
        {
            var locality = query.Locality.Trim();
            rows = rows.Where(clinic => clinic.Branches.Any(branch => branch.IsActive && branch.AddressLine.Contains(locality)));
        }

        if (!string.IsNullOrWhiteSpace(query.ClinicType) && Enum.TryParse<ClinicType>(query.ClinicType, true, out var clinicType))
        {
            rows = rows.Where(clinic => clinic.Type == clinicType);
        }

        if (query.SpecializationId is Guid specializationId)
        {
            rows = rows.Where(clinic => db.ConsultationSchedules.Any(schedule =>
                schedule.IsPublished && schedule.ClinicId == clinic.Id
                && db.Doctors.Any(doctor => doctor.Id == schedule.DoctorId && doctor.Specializations.Any(link => link.SpecializationId == specializationId))));
        }

        return rows.OrderBy(clinic => clinic.Name);
    }

    private IQueryable<Clinic> ListedClinics(DateOnly today) =>
        db.Clinics.AsNoTracking().Include(clinic => clinic.Branches).Where(clinic =>
            clinic.Status == VerificationStatus.Approved
            && clinic.Branches.Any(branch => branch.IsActive)
            && (clinic.RegistrationExpiresOn == null || clinic.RegistrationExpiresOn >= today));

    private async Task<IReadOnlyList<DiscoveredClinic>> ProjectClinics(
        IReadOnlyList<Clinic> clinics,
        DiscoveryQuery query,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var clinicIds = clinics.Select(clinic => clinic.Id).ToArray();
        var facilities = await db.ClinicFacilities.AsNoTracking().Where(item => clinicIds.Contains(item.ClinicId)).ToListAsync(cancellationToken);
        var branchIds = clinics.SelectMany(clinic => clinic.Branches).Select(branch => branch.Id).ToArray();
        var hours = await db.BranchOperatingHours.AsNoTracking().Where(item => branchIds.Contains(item.BranchId)).ToListAsync(cancellationToken);
        var closures = await db.BranchClosures.AsNoTracking().Where(item => branchIds.Contains(item.BranchId) && item.Date == today).ToListAsync(cancellationToken);
        var schedules = await db.ConsultationSchedules.AsNoTracking()
            .Where(schedule => schedule.IsPublished && schedule.ClinicId != null && clinicIds.Contains(schedule.ClinicId.Value))
            .Select(schedule => new { schedule.ClinicId, schedule.BranchId, schedule.DoctorId })
            .ToListAsync(cancellationToken);
        var specializationNames = await (
            from schedule in db.ConsultationSchedules.AsNoTracking()
            where schedule.IsPublished && schedule.ClinicId != null && clinicIds.Contains(schedule.ClinicId.Value)
            join link in db.Set<DoctorSpecialization>().AsNoTracking() on schedule.DoctorId equals link.DoctorId
            join specialization in db.Specializations.AsNoTracking() on link.SpecializationId equals specialization.Id
            select new { ClinicId = schedule.ClinicId!.Value, specialization.Name }).Distinct().ToListAsync(cancellationToken);
        var logos = await db.ProviderDocuments.AsNoTracking()
            .Where(document => document.ClinicId != null && clinicIds.Contains(document.ClinicId.Value) && document.IsCurrent && document.Category == DocumentCategory.ClinicLogo)
            .Select(document => new { document.ClinicId, document.Id })
            .ToListAsync(cancellationToken);

        var projected = clinics.Select(clinic =>
        {
            var branches = clinic.Branches.Where(branch => branch.IsActive).Select(branch =>
            {
                double? distance = query.Latitude is double lat && query.Longitude is double lng && branch.Latitude is decimal placeLat && branch.Longitude is decimal placeLng
                    ? Distance(lat, lng, (double)placeLat, (double)placeLng)
                    : null;
                var configured = hours.Any(item => item.BranchId == branch.Id);
                var closed = closures.Any(item => item.BranchId == branch.Id)
                    || hours.Any(item => item.BranchId == branch.Id && item.Day == today.DayOfWeek && item.IsClosed);
                var open = configured && !closed && hours.Any(item => item.BranchId == branch.Id && item.Day == today.DayOfWeek && !item.IsClosed);
                return new DiscoveredBranch(
                    branch.Id,
                    branch.Name,
                    branch.AddressLine,
                    branch.AddressLine2,
                    branch.City,
                    branch.State,
                    branch.PostalCode,
                    branch.PhoneNumber,
                    branch.Latitude,
                    branch.Longitude,
                    distance,
                    schedules.Count(item => item.BranchId == branch.Id),
                    null,
                    open);
            }).Where(branch => query.RadiusKm is not double radius || branch.DistanceKm is null || branch.DistanceKm <= radius)
                .OrderBy(branch => branch.DistanceKm ?? double.MaxValue)
                .ToArray();
            return new DiscoveredClinic(
                clinic.Id,
                clinic.Name ?? string.Empty,
                clinic.Type?.ToString() ?? string.Empty,
                clinic.Description,
                logos.FirstOrDefault(item => item.ClinicId == clinic.Id)?.Id,
                facilities.Where(item => item.ClinicId == clinic.Id).Select(item => item.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                specializationNames.Where(item => item.ClinicId == clinic.Id).Select(item => item.Name).Order(StringComparer.Ordinal).ToArray(),
                branches);
        }).Where(clinic => clinic.Branches.Count > 0);

        if (query.Availability == "today")
        {
            projected = projected.Where(clinic => clinic.Branches.Any(branch => branch.OpenToday));
        }

        var skip = Math.Max(0, (query.Page - 1) * query.PageSize);
        return projected.Skip(skip).Take(query.PageSize).ToArray();
    }

    private static DiscoveryQuery EmptyQuery() =>
        new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 1, 20);

    private static double Distance(double lat1, double lng1, double lat2, double lng2)
    {
        const double earth = 6371d;
        var dLat = (lat2 - lat1) * Math.PI / 180d;
        var dLng = (lng2 - lng1) * Math.PI / 180d;
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d) * Math.Pow(Math.Sin(dLng / 2), 2);
        return earth * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private IQueryable<PlaceRow> Places(DateOnly today, DiscoveryQuery query, Guid? doctorId)
    {
        var rows =
            from schedule in db.ConsultationSchedules.AsNoTracking()
            join doctor in db.Doctors.AsNoTracking() on schedule.DoctorId equals doctor.Id
            where schedule.IsPublished
                && doctor.Status == VerificationStatus.Approved
                && doctor.IsAcceptingPatients
                && doctor.Registrations.Any(registration =>
                    registration.IsCurrent && (registration.ExpiresOn == null || registration.ExpiresOn >= today))
            join clinic in db.Clinics.AsNoTracking() on schedule.ClinicId equals clinic.Id into clinics
            from clinic in clinics.DefaultIfEmpty()
            join branch in db.ClinicBranches.AsNoTracking() on schedule.BranchId equals branch.Id into branches
            from branch in branches.DefaultIfEmpty()
            where schedule.ClinicId != null
                && clinic != null
                && branch != null
                && clinic.Status == VerificationStatus.Approved
                && branch.IsActive
                && (clinic.RegistrationExpiresOn == null || clinic.RegistrationExpiresOn >= today)
                && db.DoctorClinicAffiliations.Any(link => link.DoctorId == doctor.Id && link.ClinicId == clinic.Id)
                && !db.Patients.Any(patient => patient.UserId == doctor.UserId && patient.DeletionRequestedAtUtc != null)
            select new { schedule, doctor, clinic, branch };

        if (doctorId is Guid selectedDoctor)
        {
            rows = rows.Where(row => row.doctor.Id == selectedDoctor);
        }

        if (query.SpecializationId is Guid specializationId)
        {
            rows = rows.Where(row => row.doctor.Specializations.Any(link => link.SpecializationId == specializationId));
        }

        if (query.ClinicId is Guid clinicId)
        {
            rows = rows.Where(row => row.schedule.ClinicId == clinicId);
        }

        if (query.MinFee is decimal minFee)
        {
            rows = rows.Where(row => row.schedule.ConsultationFee >= minFee);
        }

        if (query.MaxFee is decimal maxFee)
        {
            rows = rows.Where(row => row.schedule.ConsultationFee <= maxFee);
        }

        if (query.MinExperience is int experience)
        {
            rows = rows.Where(row => (row.doctor.YearsOfExperience ?? 0) >= experience);
        }

        if (!string.IsNullOrWhiteSpace(query.Gender))
        {
            var gender = query.Gender.Trim();
            rows = rows.Where(row => row.doctor.Gender == gender);
        }

        if (!string.IsNullOrWhiteSpace(query.Language))
        {
            var language = query.Language.Trim();
            rows = rows.Where(row => row.doctor.Languages.Any(item => item.Code == language));
        }

        if (!string.IsNullOrWhiteSpace(query.Qualification))
        {
            var qualification = query.Qualification.Trim();
            rows = rows.Where(row => row.doctor.Qualifications.Any(item => item.Name.Contains(qualification)));
        }

        if (!string.IsNullOrWhiteSpace(query.PostalCode))
        {
            var postalCode = query.PostalCode.Trim();
            rows = rows.Where(row => row.branch != null && row.branch.PostalCode == postalCode);
        }

        if (!string.IsNullOrWhiteSpace(query.Locality))
        {
            var locality = query.Locality.Trim();
            rows = rows.Where(row => row.branch != null && (row.branch.AddressLine.Contains(locality) || (row.branch.AddressLine2 != null && row.branch.AddressLine2.Contains(locality))));
        }

        if (query.Availability is "today" or "tomorrow")
        {
            var day = query.Availability == "today" ? today : today.AddDays(1);
            var dayOfWeek = day.DayOfWeek;
            rows = rows.Where(row =>
                (row.schedule.EffectiveFrom == null || row.schedule.EffectiveFrom <= day)
                && (row.schedule.EffectiveTo == null || row.schedule.EffectiveTo >= day)
                && !row.schedule.Closures.Any(item => item.Date == day)
                && (row.schedule.Hours.Any(item => item.Day == dayOfWeek) || row.schedule.Extras.Any(item => item.Date == day)));
        }
        else if (query.Availability == "week")
        {
            var end = today.AddDays(6);
            rows = rows.Where(row =>
                row.schedule.Hours.Any()
                && (row.schedule.EffectiveFrom == null || row.schedule.EffectiveFrom <= end)
                && (row.schedule.EffectiveTo == null || row.schedule.EffectiveTo >= today));
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();
            rows = rows.Where(row => row.branch != null && row.branch.City.Contains(city));
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            rows = rows.Where(row =>
                (row.doctor.DisplayName != null && row.doctor.DisplayName.Contains(text))
                || (row.doctor.FirstName != null && row.doctor.FirstName.Contains(text))
                || (row.doctor.LastName != null && row.doctor.LastName.Contains(text))
                || (row.clinic != null && row.clinic.Name != null && row.clinic.Name.Contains(text))
                || (row.branch != null && row.branch.City.Contains(text))
                || (row.branch != null && row.branch.AddressLine.Contains(text))
                || (row.branch != null && row.branch.PostalCode.Contains(text))
                || row.doctor.Qualifications.Any(item => item.Name.Contains(text))
                || db.Specializations.Any(item =>
                    row.doctor.Specializations.Any(link => link.SpecializationId == item.Id) && item.Name.Contains(text)));
        }

        if (query.Latitude is double latitude && query.Longitude is double longitude && query.RadiusKm is double radius)
        {
            var latDelta = radius / 111d;
            var cosine = Math.Cos(latitude * Math.PI / 180d);
            var lngDelta = Math.Abs(cosine) < 0.01 ? 180d : radius / (111d * cosine);
            var minLat = (decimal)(latitude - latDelta);
            var maxLat = (decimal)(latitude + latDelta);
            var minLng = (decimal)(longitude - lngDelta);
            var maxLng = (decimal)(longitude + lngDelta);
            rows = rows.Where(row =>
                row.branch != null
                && row.branch.Latitude != null
                && row.branch.Longitude != null
                && row.branch.Latitude >= minLat
                && row.branch.Latitude <= maxLat
                && row.branch.Longitude >= minLng
                && row.branch.Longitude <= maxLng);
        }

        return rows.Select(row => new PlaceRow(
            row.schedule.Id,
            row.doctor.Id,
            row.doctor.DisplayName ?? string.Empty,
            db.Specializations
                .Where(item => row.doctor.Specializations.Any(link => link.IsPrimary && link.SpecializationId == item.Id))
                .Select(item => item.Name)
                .FirstOrDefault() ?? string.Empty,
            row.doctor.YearsOfExperience ?? 0,
            row.doctor.Documents
                .Where(document => document.IsCurrent && document.Category == DocumentCategory.ProfilePhoto)
                .Select(document => (Guid?)document.Id)
                .FirstOrDefault(),
            row.clinic == null ? null : row.clinic.Id,
            row.clinic == null ? null : row.clinic.Name,
            row.branch == null ? null : row.branch.Id,
            row.branch == null ? null : row.branch.Name,
            row.branch == null ? null : row.branch.AddressLine,
            row.branch == null ? null : row.branch.City,
            row.branch == null ? null : row.branch.Latitude,
            row.branch == null ? null : row.branch.Longitude,
            row.schedule.ConsultationFee,
            row.schedule.SlotDurationMinutes,
            row.branch == null ? null : row.branch.PostalCode,
            row.branch == null ? null : row.branch.PhoneNumber));
    }

    private IQueryable<ConsultationSchedule> Schedules(bool tracking)
    {
        var query = tracking ? db.ConsultationSchedules : db.ConsultationSchedules.AsNoTracking();
        return query.AsSplitQuery()
            .Include(schedule => schedule.Hours)
            .Include(schedule => schedule.Breaks)
            .Include(schedule => schedule.Closures)
            .Include(schedule => schedule.Extras);
    }

    private sealed record PlaceRow(
        Guid ScheduleId,
        Guid DoctorId,
        string DisplayName,
        string PrimarySpecialization,
        int YearsOfExperience,
        Guid? PhotoDocumentId,
        Guid? ClinicId,
        string? ClinicName,
        Guid? BranchId,
        string? BranchName,
        string? AddressLine,
        string? City,
        decimal? Latitude,
        decimal? Longitude,
        decimal ConsultationFee,
        int SlotDurationMinutes,
        string? PostalCode,
        string? PhoneNumber)
    {
        public DiscoveryPlace ToPlace(double? originLatitude, double? originLongitude)
        {
            double? distance = null;
            if (originLatitude is double lat && originLongitude is double lng && Latitude is decimal placeLat && Longitude is decimal placeLng)
            {
                distance = DistanceKm(lat, lng, (double)placeLat, (double)placeLng);
            }

            return new DiscoveryPlace(
                ScheduleId,
                DoctorId,
                DisplayName,
                PrimarySpecialization,
                YearsOfExperience,
                PhotoDocumentId,
                ClinicId,
                ClinicName,
                BranchId,
                BranchName,
                AddressLine,
                City,
                Latitude,
                Longitude,
                ConsultationFee,
                SlotDurationMinutes,
                distance,
                PostalCode,
                PhoneNumber,
                null);
        }

        private static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double earth = 6371d;
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLng = DegreesToRadians(lng2 - lng1);
            var a = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) * Math.Pow(Math.Sin(dLng / 2), 2);
            return earth * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
    }
}
