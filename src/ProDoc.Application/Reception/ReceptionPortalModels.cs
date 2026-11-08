using ProDoc.Application.Appointments;
using ProDoc.Application.Queue;

namespace ProDoc.Application.Reception;

public sealed record ReceptionDashboard(
    Guid ClinicId,
    string ClinicName,
    Guid? BranchId,
    string? BranchName,
    ReceptionAppointmentCounts Appointments,
    ReceptionQueueCounts Queue,
    ReceptionPaymentCounts Payments,
    IReadOnlyList<DeskDoctorOverview> Doctors,
    IReadOnlyList<AppointmentDetails> Timeline,
    AppointmentDetails? NextPatient,
    IReadOnlyList<NotificationDetails> Notifications,
    IReadOnlyList<ReceptionChartPoint> HourlyBookings);

public sealed record ReceptionAppointmentCounts(
    int Today,
    int Waiting,
    int CheckedIn,
    int InConsult,
    int Completed,
    int Cancelled,
    int NoShow,
    int WalkIn);

public sealed record ReceptionQueueCounts(
    int Waiting,
    int Called,
    int InConsultation,
    int Completed,
    int WalkIns);

public sealed record ReceptionPaymentCounts(
    int Pending,
    int Paid,
    decimal PendingAmount,
    decimal CollectedToday);

public sealed record DeskDoctorOverview(
    Guid DoctorId,
    string DisplayName,
    int TodayAppointments,
    int Waiting,
    int InConsultation,
    AppointmentDetails? Next);

public sealed record ReceptionChartPoint(string Label, int Value);

public sealed record ReceptionPatientRow(
    Guid? AccountUserId,
    string FullName,
    string? PhoneMasked,
    int VisitCount,
    DateTimeOffset LastVisitAtUtc,
    string LastStatus,
    Guid LastAppointmentId,
    Guid DoctorId,
    string DoctorName);

public sealed record ReceptionPatientProfile(
    Guid? AccountUserId,
    string FullName,
    string? PhoneMasked,
    IReadOnlyList<AppointmentDetails> History);

public sealed record ReceptionClinicInfo(
    Guid ClinicId,
    string Name,
    string? PhoneNumber,
    string? Type,
    IReadOnlyList<ReceptionBranchInfo> Branches,
    IReadOnlyList<DeskDoctor> Doctors);

public sealed record ReceptionBranchInfo(
    Guid Id,
    string Name,
    string AddressLine,
    string? AddressLine2,
    string? Landmark,
    string? Locality,
    string City,
    string? District,
    string State,
    string PostalCode,
    string Country,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    string? FormattedAddress,
    string? MapsUrl,
    string? DirectionsUrl,
    bool LocationVerified,
    bool IsPrimary,
    bool IsActive);

public sealed record ReceptionReportSummary(
    Guid ClinicId,
    DateOnly From,
    DateOnly To,
    int Appointments,
    int WalkIns,
    int NoShows,
    int Completed,
    int Cancelled,
    decimal Collected,
    IReadOnlyList<ReceptionDoctorReportRow> ByDoctor);

public sealed record ReceptionDoctorReportRow(
    Guid DoctorId,
    string DoctorName,
    int Appointments,
    int WalkIns,
    int NoShows,
    int Completed,
    decimal Collected);

public sealed record ReceptionRecordsStatus(
    Guid AppointmentId,
    bool HasConsultation,
    string? ConsultationStatus,
    bool PrescriptionReady,
    string? PrescriptionStatus,
    int DocumentCount);

public sealed record ReceptionDeskPaymentRow(
    Guid AppointmentId,
    string Reference,
    string PatientName,
    string DoctorName,
    decimal Fee,
    string Currency,
    string PaymentStatus,
    DateTimeOffset StartsAtUtc);
