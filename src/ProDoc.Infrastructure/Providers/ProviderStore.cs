using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProDoc.Application.Providers;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Providers;
using ProDoc.Infrastructure.Identity;
using ProDoc.Infrastructure.Persistence;

namespace ProDoc.Infrastructure.Providers;

public sealed class ProviderStore(ProDocDbContext db, UserManager<ApplicationUser> users) : IProviderStore
{
    public async Task<IReadOnlyList<Specialization>> ListSpecializationsAsync(CancellationToken cancellationToken) =>
        await db.Specializations.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task<bool> SpecializationsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return false;
        }

        var count = await db.Specializations.CountAsync(
            item => item.IsActive && distinct.Contains(item.Id),
            cancellationToken);
        return count == distinct.Length;
    }

    public Task<Doctor?> FindDoctorByUserIdAsync(Guid userId, bool tracking, CancellationToken cancellationToken) =>
        Doctors(tracking).SingleOrDefaultAsync(doctor => doctor.UserId == userId, cancellationToken);

    public Task<Doctor?> FindDoctorAsync(Guid doctorId, bool tracking, CancellationToken cancellationToken) =>
        Doctors(tracking).SingleOrDefaultAsync(doctor => doctor.Id == doctorId, cancellationToken);

    public Task AddDoctorAsync(Doctor doctor, CancellationToken cancellationToken)
    {
        db.Doctors.Add(doctor);
        return Task.CompletedTask;
    }

    public Task<Clinic?> FindClinicAsync(Guid clinicId, bool tracking, CancellationToken cancellationToken) =>
        Clinics(tracking).SingleOrDefaultAsync(clinic => clinic.Id == clinicId, cancellationToken);

    public async Task<IReadOnlyList<Clinic>> ListClinicsByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
        await Clinics(tracking: true)
            .Where(clinic => clinic.OwnerUserId == ownerUserId)
            .OrderBy(clinic => clinic.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task AddClinicAsync(Clinic clinic, CancellationToken cancellationToken)
    {
        db.Clinics.Add(clinic);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<DoctorClinicAffiliation>> ListAffiliationsAsync(Guid doctorId, CancellationToken cancellationToken) =>
        await db.DoctorClinicAffiliations
            .Where(item => item.DoctorId == doctorId)
            .OrderByDescending(item => item.IsPrimary)
            .ToListAsync(cancellationToken);

    public Task<DoctorClinicAffiliation?> FindAffiliationAsync(Guid doctorId, Guid clinicId, CancellationToken cancellationToken) =>
        db.DoctorClinicAffiliations.SingleOrDefaultAsync(
            item => item.DoctorId == doctorId && item.ClinicId == clinicId,
            cancellationToken);

    public Task AddAffiliationAsync(DoctorClinicAffiliation affiliation, CancellationToken cancellationToken)
    {
        db.DoctorClinicAffiliations.Add(affiliation);
        return Task.CompletedTask;
    }

    public void RemoveAffiliation(DoctorClinicAffiliation affiliation) => db.DoctorClinicAffiliations.Remove(affiliation);

    public async Task<IReadOnlyList<AffiliationModel>> ListAffiliationDetailsAsync(
        Guid doctorId,
        bool approvedClinicsOnly,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var links = db.DoctorClinicAffiliations.AsNoTracking().Where(link => link.DoctorId == doctorId);
        if (approvedClinicsOnly)
        {
            links =
                from link in links
                join clinic in db.Clinics.AsNoTracking() on link.ClinicId equals clinic.Id
                where clinic.Status == VerificationStatus.Approved
                    && (clinic.RegistrationExpiresOn == null || clinic.RegistrationExpiresOn >= today)
                    && clinic.Branches.Any(branch => branch.IsActive)
                select link;
        }

        var rows = await (
            from link in links
            join clinic in db.Clinics.AsNoTracking() on link.ClinicId equals clinic.Id
            select new { link, clinic }).ToListAsync(cancellationToken);

        var branchIds = rows
            .Select(row => row.link.BranchId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var primaryBranchIds = await db.ClinicBranches.AsNoTracking()
            .Where(branch => rows.Select(row => row.clinic.Id).Contains(branch.ClinicId) && branch.IsActive && branch.IsPrimary)
            .Select(branch => new { branch.ClinicId, branch.Id })
            .ToListAsync(cancellationToken);
        foreach (var primary in primaryBranchIds)
        {
            if (!branchIds.Contains(primary.Id))
            {
                branchIds = branchIds.Append(primary.Id).ToArray();
            }
        }

        var branches = await db.ClinicBranches.AsNoTracking()
            .Where(branch => branchIds.Contains(branch.Id))
            .ToDictionaryAsync(branch => branch.Id, cancellationToken);

        return rows
            .OrderByDescending(row => row.link.IsPrimary)
            .Select(row =>
            {
                ClinicBranch? branch = null;
                if (row.link.BranchId is Guid linkedBranchId && branches.TryGetValue(linkedBranchId, out var linked))
                {
                    branch = linked;
                }
                else
                {
                    var primaryId = primaryBranchIds.FirstOrDefault(item => item.ClinicId == row.clinic.Id)?.Id;
                    if (primaryId is Guid id && branches.TryGetValue(id, out var primaryBranch))
                    {
                        branch = primaryBranch;
                    }
                }

                var useOverride = row.link.HasPracticeLocationOverride;
                var addressLine = useOverride ? row.link.PracticeAddressLine : branch?.AddressLine;
                var addressLine2 = useOverride ? row.link.PracticeAddressLine2 : branch?.AddressLine2;
                var landmark = useOverride ? row.link.PracticeLandmark : branch?.Landmark;
                var locality = useOverride ? row.link.PracticeLocality : branch?.Locality;
                var city = useOverride ? row.link.PracticeCity : branch?.City;
                var district = useOverride ? row.link.PracticeDistrict : branch?.District;
                var state = useOverride ? row.link.PracticeState : branch?.State;
                var postalCode = useOverride ? row.link.PracticePostalCode : branch?.PostalCode;
                var country = useOverride ? row.link.PracticeCountry : branch?.Country;
                var latitude = useOverride ? row.link.PracticeLatitude : branch?.Latitude;
                var longitude = useOverride ? row.link.PracticeLongitude : branch?.Longitude;
                var googlePlaceId = useOverride ? row.link.PracticeGooglePlaceId : branch?.GooglePlaceId;
                var formatted = PracticeLocation.FormatFull(
                    addressLine,
                    addressLine2,
                    landmark,
                    locality,
                    city,
                    district,
                    state,
                    postalCode,
                    country);
                var compact = PracticeLocation.FormatCompact(locality, city, postalCode);

                return new AffiliationModel(
                    row.link.Id,
                    row.clinic.Id,
                    row.clinic.Name ?? row.clinic.Reference,
                    row.clinic.Status.ToString(),
                    row.link.BranchId ?? branch?.Id,
                    branch?.Name,
                    row.link.Kind.ToString(),
                    row.link.IsPrimary,
                    addressLine,
                    addressLine2,
                    landmark,
                    locality,
                    city,
                    district,
                    state,
                    postalCode,
                    country,
                    latitude,
                    longitude,
                    googlePlaceId,
                    string.IsNullOrWhiteSpace(formatted) ? null : formatted,
                    string.IsNullOrWhiteSpace(compact) ? null : compact,
                    null,
                    null,
                    useOverride,
                    latitude is not null && longitude is not null);
            })
            .ToArray();
    }

    public Task<ProviderDocument?> FindDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        db.ProviderDocuments.AsNoTracking().SingleOrDefaultAsync(document => document.Id == documentId, cancellationToken);

    public async Task<string?> FindUserPhoneAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.PhoneNumber)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorCard>> SearchDoctorsAsync(
        string? query,
        Guid? specializationId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var doctors = BookableDoctors(today);
        if (specializationId is Guid selected)
        {
            doctors = doctors.Where(doctor => doctor.Specializations.Any(link => link.SpecializationId == selected));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var text = query.Trim().ToLower();
            doctors = doctors.Where(doctor =>
                (doctor.DisplayName != null && doctor.DisplayName.ToLower().Contains(text))
                || (doctor.FirstName != null && doctor.FirstName.ToLower().Contains(text))
                || (doctor.LastName != null && doctor.LastName.ToLower().Contains(text))
                || (((doctor.FirstName ?? string.Empty) + " " + (doctor.LastName ?? string.Empty)).ToLower().Contains(text))
                || doctor.Qualifications.Any(item => item.Name.ToLower().Contains(text))
                || doctor.Specializations.Any(link =>
                    db.Specializations.Any(item => item.Id == link.SpecializationId && item.Name.ToLower().Contains(text)))
                || db.DoctorClinicAffiliations.Any(link =>
                    link.DoctorId == doctor.Id
                    && (db.Clinics.Any(clinic =>
                            clinic.Id == link.ClinicId
                            && clinic.Name != null
                            && clinic.Name.ToLower().Contains(text))
                        || db.ClinicBranches.Any(branch =>
                            branch.ClinicId == link.ClinicId
                            && branch.IsActive
                            && branch.Name.ToLower().Contains(text)))));
        }

        return await doctors
            .OrderBy(doctor => doctor.DisplayName)
            .Take(50)
            .Select(doctor => new DoctorCard(
                doctor.Id,
                doctor.DisplayName ?? (((doctor.FirstName ?? string.Empty) + " " + (doctor.LastName ?? string.Empty)).Trim()),
                doctor.YearsOfExperience,
                db.Specializations
                    .Where(item => doctor.Specializations.Any(link => link.IsPrimary && link.SpecializationId == item.Id))
                    .Select(item => item.Name)
                    .FirstOrDefault() ?? string.Empty,
                doctor.Documents
                    .Where(document => document.IsCurrent && document.Category == DocumentCategory.ProfilePhoto)
                    .Select(document => (Guid?)document.Id)
                    .FirstOrDefault(),
                db.DoctorClinicAffiliations
                    .Where(link => link.DoctorId == doctor.Id)
                    .OrderByDescending(link => link.IsPrimary)
                    .Select(link => db.Clinics.Where(clinic => clinic.Id == link.ClinicId).Select(clinic => clinic.Name).FirstOrDefault())
                    .FirstOrDefault(),
                db.DoctorClinicAffiliations
                    .Where(link => link.DoctorId == doctor.Id)
                    .OrderByDescending(link => link.IsPrimary)
                    .Select(link => db.ClinicBranches
                        .Where(branch => branch.ClinicId == link.ClinicId && branch.IsActive)
                        .OrderByDescending(branch => branch.IsPrimary)
                        .Select(branch => branch.City)
                        .FirstOrDefault())
                    .FirstOrDefault(),
                doctor.Qualifications
                    .OrderBy(item => item.Name)
                    .Select(item => item.Name)
                    .FirstOrDefault(),
                true))
            .ToListAsync(cancellationToken);
    }

    public async Task<PublicDoctor?> FindPublicDoctorAsync(Guid doctorId, DateOnly today, CancellationToken cancellationToken)
    {
        var doctor = await Doctors(tracking: false).SingleOrDefaultAsync(item => item.Id == doctorId, cancellationToken);
        if (doctor is null || !doctor.IsPubliclyBookable(today))
        {
            return null;
        }

        var specializations = await db.Specializations.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
        var primary = doctor.Specializations.SingleOrDefault(link => link.IsPrimary);
        var names = doctor.Specializations
            .Select(link => specializations.TryGetValue(link.SpecializationId, out var item) ? item.Name : null)
            .Where(name => name is not null)
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
        var affiliations = await ListAffiliationDetailsAsync(doctor.Id, approvedClinicsOnly: true, today, cancellationToken);
        return new PublicDoctor(
            doctor.Id,
            doctor.DisplayName ?? $"{doctor.FirstName} {doctor.LastName}".Trim(),
            doctor.Biography,
            doctor.YearsOfExperience,
            primary is not null && specializations.TryGetValue(primary.SpecializationId, out var primaryName) ? primaryName.Name : string.Empty,
            names,
            doctor.Qualifications.Select(item => item.Name).Order(StringComparer.Ordinal).ToArray(),
            doctor.Languages.Select(item => item.Code).Order(StringComparer.Ordinal).ToArray(),
            doctor.Documents.SingleOrDefault(document => document.IsCurrent && document.Category == DocumentCategory.ProfilePhoto)?.Id,
            affiliations);
    }

    public async Task<IReadOnlyList<ClinicCard>> SearchClinicsAsync(string? query, DateOnly today, CancellationToken cancellationToken)
    {
        var clinics = ListedClinics(today);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var text = query.Trim().ToLower();
            clinics = clinics.Where(clinic =>
                (clinic.Name != null && clinic.Name.ToLower().Contains(text))
                || clinic.Branches.Any(branch =>
                    branch.IsActive
                    && (branch.Name.ToLower().Contains(text)
                        || branch.City.ToLower().Contains(text)
                        || branch.AddressLine.ToLower().Contains(text)
                        || branch.PostalCode.ToLower().Contains(text))));
        }

        var rows = await clinics
            .OrderBy(clinic => clinic.Name)
            .Take(50)
            .Select(clinic => new
            {
                clinic.Id,
                clinic.Name,
                clinic.Type,
                BranchName = clinic.Branches
                    .Where(branch => branch.IsActive)
                    .OrderByDescending(branch => branch.IsPrimary)
                    .Select(branch => branch.Name)
                    .FirstOrDefault(),
                City = clinic.Branches
                    .Where(branch => branch.IsActive)
                    .OrderByDescending(branch => branch.IsPrimary)
                    .Select(branch => branch.City)
                    .FirstOrDefault(),
                AddressLine = clinic.Branches
                    .Where(branch => branch.IsActive)
                    .OrderByDescending(branch => branch.IsPrimary)
                    .Select(branch => branch.AddressLine)
                    .FirstOrDefault(),
                LogoId = clinic.Documents
                    .Where(document => document.IsCurrent && document.Category == DocumentCategory.ClinicLogo)
                    .Select(document => (Guid?)document.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var clinicIds = rows.Select(row => row.Id).ToArray();
        var associated = clinicIds.Length == 0
            ? []
            : await (
                from link in db.DoctorClinicAffiliations.AsNoTracking()
                where clinicIds.Contains(link.ClinicId)
                join doctor in BookableDoctors(today) on link.DoctorId equals doctor.Id
                select new
                {
                    link.ClinicId,
                    Name = doctor.DisplayName
                        ?? (((doctor.FirstName ?? string.Empty) + " " + (doctor.LastName ?? string.Empty)).Trim())
                }).ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var doctors = associated
                    .Where(item => item.ClinicId == row.Id && !string.IsNullOrWhiteSpace(item.Name))
                    .Select(item => item.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToArray();
                var location = string.Join(
                    ", ",
                    new[] { row.AddressLine, row.City }.Where(part => !string.IsNullOrWhiteSpace(part)));
                return new ClinicCard(
                    row.Id,
                    row.Name ?? string.Empty,
                    row.Type?.ToString() ?? string.Empty,
                    row.City ?? string.Empty,
                    row.LogoId,
                    row.BranchName,
                    string.IsNullOrWhiteSpace(location) ? row.City : location,
                    doctors);
            })
            .ToArray();
    }

    public async Task<PublicClinic?> FindPublicClinicAsync(Guid clinicId, DateOnly today, CancellationToken cancellationToken)
    {
        var clinic = await Clinics(tracking: false).SingleOrDefaultAsync(item => item.Id == clinicId, cancellationToken);
        if (clinic is null || !clinic.IsPubliclyListed(today))
        {
            return null;
        }

        return new PublicClinic(
            clinic.Id,
            clinic.Name ?? clinic.Reference,
            clinic.Type?.ToString() ?? string.Empty,
            clinic.Description,
            clinic.PhoneNumber,
            clinic.Website,
            clinic.Documents.SingleOrDefault(document => document.IsCurrent && document.Category == DocumentCategory.ClinicLogo)?.Id,
            clinic.Branches.Where(branch => branch.IsActive).OrderByDescending(branch => branch.IsPrimary).Select(ProviderMapper.ToBranch).ToArray());
    }

    public async Task<IReadOnlyList<ReviewQueueItem>> ListReviewsAsync(
        string? kind,
        VerificationStatus? status,
        CancellationToken cancellationToken)
    {
        var includeDoctors = kind is null or "doctor";
        var includeClinics = kind is null or "clinic";
        var items = new List<ReviewQueueItem>();
        if (includeDoctors)
        {
            var doctors = db.Doctors.AsNoTracking().AsQueryable();
            doctors = status is VerificationStatus selected
                ? doctors.Where(doctor => doctor.Status == selected)
                : doctors.Where(doctor => doctor.Status == VerificationStatus.Submitted || doctor.Status == VerificationStatus.UnderReview);
            var doctorRows = await doctors
                .OrderBy(doctor => doctor.SubmittedAtUtc)
                .Take(100)
                .Select(doctor => new
                {
                    doctor.Id,
                    doctor.Reference,
                    doctor.DisplayName,
                    doctor.FirstName,
                    doctor.LastName,
                    doctor.Status,
                    doctor.SubmittedAtUtc
                })
                .ToListAsync(cancellationToken);
            items.AddRange(doctorRows.Select(doctor => new ReviewQueueItem(
                "doctor",
                doctor.Id,
                doctor.Reference,
                string.IsNullOrWhiteSpace(doctor.DisplayName)
                    ? $"{doctor.FirstName} {doctor.LastName}".Trim()
                    : doctor.DisplayName,
                doctor.Status.ToString(),
                doctor.SubmittedAtUtc)));
        }

        if (includeClinics)
        {
            var clinics = db.Clinics.AsNoTracking().AsQueryable();
            clinics = status is VerificationStatus selected
                ? clinics.Where(clinic => clinic.Status == selected)
                : clinics.Where(clinic => clinic.Status == VerificationStatus.Submitted || clinic.Status == VerificationStatus.UnderReview);
            var clinicRows = await clinics
                .OrderBy(clinic => clinic.SubmittedAtUtc)
                .Take(100)
                .Select(clinic => new
                {
                    clinic.Id,
                    clinic.Reference,
                    clinic.Name,
                    clinic.Status,
                    clinic.SubmittedAtUtc
                })
                .ToListAsync(cancellationToken);
            items.AddRange(clinicRows.Select(clinic => new ReviewQueueItem(
                "clinic",
                clinic.Id,
                clinic.Reference,
                clinic.Name ?? clinic.Reference,
                clinic.Status.ToString(),
                clinic.SubmittedAtUtc)));
        }

        return items.OrderBy(item => item.SubmittedAtUtc).Take(100).ToArray();
    }

    public async Task GrantRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw DomainException.NotFound("account_not_found", "Account was not found.");
        if (await users.IsInRoleAsync(user, roleName))
        {
            return;
        }

        var result = await users.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not grant the {roleName} role.");
        }
    }

    public Task AddAuditAsync(AuditLog log, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(log);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw DomainException.Conflict("concurrency_conflict", "This profile was updated. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw DomainException.Conflict("duplicate_record", "That record already exists.");
        }
    }

    private IQueryable<Doctor> Doctors(bool tracking)
    {
        var query = tracking ? db.Doctors : db.Doctors.AsNoTracking();
        return query
            .AsSplitQuery()
            .Include(doctor => doctor.Qualifications)
            .Include(doctor => doctor.Specializations)
            .Include(doctor => doctor.Languages)
            .Include(doctor => doctor.Expertise)
            .Include(doctor => doctor.Registrations)
            .Include(doctor => doctor.Documents);
    }

    private IQueryable<Clinic> Clinics(bool tracking)
    {
        var query = tracking ? db.Clinics : db.Clinics.AsNoTracking();
        return query.AsSplitQuery().Include(clinic => clinic.Branches).Include(clinic => clinic.Documents);
    }

    private IQueryable<Doctor> BookableDoctors(DateOnly today) =>
        db.Doctors.AsNoTracking().Where(doctor =>
            doctor.Status == VerificationStatus.Approved
            && doctor.IsAcceptingPatients
            && doctor.Registrations.Any(registration =>
                registration.IsCurrent && (registration.ExpiresOn == null || registration.ExpiresOn >= today))
            && !db.Patients.Any(patient => patient.UserId == doctor.UserId && patient.DeletionRequestedAtUtc != null)
            && !db.Users.Any(user =>
                user.Id == doctor.UserId
                && (user.SuspendedAtUtc != null || user.PermanentlyDeletedAtUtc != null))
            && db.ConsultationSchedules.Any(schedule =>
                schedule.DoctorId == doctor.Id
                && schedule.IsPublished
                && db.Clinics.Any(clinic =>
                    clinic.Id == schedule.ClinicId
                    && clinic.Status == VerificationStatus.Approved)
                && db.ClinicBranches.Any(branch =>
                    branch.Id == schedule.BranchId
                    && branch.IsActive)
                && db.DoctorClinicAffiliations.Any(link =>
                    link.DoctorId == doctor.Id
                    && link.ClinicId == schedule.ClinicId)));

    private IQueryable<Clinic> ListedClinics(DateOnly today) =>
        db.Clinics.AsNoTracking().Where(clinic =>
            clinic.Status == VerificationStatus.Approved
            && clinic.Branches.Any(branch => branch.IsActive)
            && (clinic.RegistrationExpiresOn == null || clinic.RegistrationExpiresOn >= today));

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && sql.Number is 2601 or 2627;
}
