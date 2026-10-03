using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Features.Bookings;
using DiagnX.Api.Services;
using DiagnX.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Controllers.PatientApp;

public sealed record PatientProfileDto(Guid Id, string? Name, string Phone, string? Email, int? Age, string? Gender, string? City, bool IsProfileComplete);

/// <summary>Profile, saved addresses, family members and push-token registration.</summary>
[Route("api/v1/patient")]
[ApiExplorerSettings(GroupName = "patient")]
[Authorize(Roles = Roles.Patient)]
public sealed class ProfileController(AppDbContext db, DeviceTokenService devices) : ApiControllerBase
{
    private static readonly string[] Genders = { "Male", "Female", "Other" };

    public static PatientProfileDto ToDto(Patient p) =>
        new(p.Id, p.Name, BookingWorkflow.FormatPhone(p.Phone), p.Email, p.Age, p.Gender, p.City, !string.IsNullOrWhiteSpace(p.Name));

    public sealed class UpdateProfileRequest
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public string? City { get; set; }
    }

    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<PatientProfileDto>>> Get() =>
        Ok(ToDto(await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == UserId) ?? throw ApiException.Unauthorized()));

    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<PatientProfileDto>>> Update(UpdateProfileRequest req)
    {
        var e = new FieldErrors();
        e.RequireText("name", req.Name, 2, 100, "Enter your name");
        if (!string.IsNullOrWhiteSpace(req.Email) && !IndianIds.IsEmail(req.Email)) e.Add("email", ErrorCodes.InvalidEmail, "Enter a valid email address");
        if (req.Age is < 0 or > 120) e.Add("age", ErrorCodes.InvalidValue, "Enter a valid age");
        if (req.Gender != null && !Genders.Contains(req.Gender)) e.Add("gender", ErrorCodes.InvalidValue, "Gender must be Male, Female or Other");
        e.MaxLength("city", req.City, 80);
        e.ThrowIfAny();

        var p = await db.Patients.FirstOrDefaultAsync(x => x.Id == UserId) ?? throw ApiException.Unauthorized();
        p.Name = req.Name!.Trim();
        p.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        p.Age = (short?)req.Age;
        p.Gender = req.Gender;
        p.City = string.IsNullOrWhiteSpace(req.City) ? null : req.City.Trim();
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(p));
    }

    // ------------------------------------------------------------ addresses

    public sealed record AddressDto(Guid Id, string Label, string Line1, string? Line2, string? Landmark, string City, string? State,
        string Pincode, decimal? Latitude, decimal? Longitude, bool IsDefault);

    public sealed class AddressRequest
    {
        public string? Label { get; set; }
        public string? Line1 { get; set; }
        public string? Line2 { get; set; }
        public string? Landmark { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public bool IsDefault { get; set; }
    }

    private static AddressDto ToDto(PatientAddress a) =>
        new(a.Id, a.Label, a.Line1, a.Line2, a.Landmark, a.City, a.State, a.Pincode, a.Latitude, a.Longitude, a.IsDefault);

    [HttpGet("addresses")]
    public async Task<ActionResult<ApiResponse<List<AddressDto>>>> Addresses() =>
        Ok((await db.PatientAddresses.AsNoTracking().Where(a => a.PatientId == UserId && !a.IsDeleted)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAt).ToListAsync()).Select(ToDto).ToList());

    [HttpPost("addresses")]
    public async Task<ActionResult<ApiResponse<AddressDto>>> AddAddress(AddressRequest req)
    {
        Validate(req);
        var a = new PatientAddress { Id = Guid.NewGuid(), PatientId = UserId, CreatedAt = DateTime.UtcNow };
        Apply(a, req);
        db.PatientAddresses.Add(a);
        await ApplyDefault(a, req.IsDefault);
        await db.SaveChangesAsync();
        return Ok(ToDto(a));
    }

    [HttpPut("addresses/{id:guid}")]
    public async Task<ActionResult<ApiResponse<AddressDto>>> UpdateAddress(Guid id, AddressRequest req)
    {
        Validate(req);
        var a = await db.PatientAddresses.FirstOrDefaultAsync(x => x.Id == id && x.PatientId == UserId && !x.IsDeleted)
                ?? throw ApiException.NotFound("Address not found");
        Apply(a, req);
        await ApplyDefault(a, req.IsDefault);
        await db.SaveChangesAsync();
        return Ok(ToDto(a));
    }

    [HttpDelete("addresses/{id:guid}")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> DeleteAddress(Guid id)
    {
        var a = await db.PatientAddresses.FirstOrDefaultAsync(x => x.Id == id && x.PatientId == UserId && !x.IsDeleted)
                ?? throw ApiException.NotFound("Address not found");
        a.IsDeleted = true; // bookings keep their own address snapshot
        if (a.IsDefault)
        {
            a.IsDefault = false;
            var next = await db.PatientAddresses.Where(x => x.PatientId == UserId && !x.IsDeleted && x.Id != id)
                .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync();
            if (next != null) next.IsDefault = true;
        }
        await db.SaveChangesAsync();
        return Success();
    }

    private static void Validate(AddressRequest r)
    {
        var e = new FieldErrors();
        e.RequireText("label", r.Label, 1, 30, "Give this address a name, e.g. Home");
        e.RequireText("line1", r.Line1, 3, 150, "Enter the flat / house and building");
        e.MaxLength("line2", r.Line2, 150);
        e.MaxLength("landmark", r.Landmark, 100);
        e.RequireText("city", r.City, 2, 80, "Enter the city");
        if (!IndianIds.IsPincode(r.Pincode)) e.Add("pincode", ErrorCodes.InvalidPincode, "Enter a valid 6-digit PIN code");
        if ((r.Latitude == null) != (r.Longitude == null) || r.Latitude is < -90 or > 90 || r.Longitude is < -180 or > 180)
            e.Add("latitude", ErrorCodes.InvalidLocation, "Invalid location");
        e.ThrowIfAny();
    }

    private static void Apply(PatientAddress a, AddressRequest r)
    {
        a.Label = r.Label!.Trim();
        a.Line1 = r.Line1!.Trim();
        a.Line2 = string.IsNullOrWhiteSpace(r.Line2) ? null : r.Line2.Trim();
        a.Landmark = string.IsNullOrWhiteSpace(r.Landmark) ? null : r.Landmark.Trim();
        a.City = r.City!.Trim();
        a.State = string.IsNullOrWhiteSpace(r.State) ? null : r.State.Trim();
        a.Pincode = r.Pincode!;
        a.Latitude = r.Latitude;
        a.Longitude = r.Longitude;
    }

    /// <summary>Exactly one default address; the first address is always default.</summary>
    private async Task ApplyDefault(PatientAddress a, bool wantDefault)
    {
        var others = await db.PatientAddresses.Where(x => x.PatientId == UserId && !x.IsDeleted && x.Id != a.Id).ToListAsync();
        if (wantDefault || others.Count == 0 || !others.Any(o => o.IsDefault))
        {
            a.IsDefault = true;
            foreach (var o in others) o.IsDefault = false;
        }
        else a.IsDefault = false;
    }

    // ------------------------------------------------------------ family members

    public sealed record FamilyMemberDto(Guid Id, string Name, string Relation, int? Age, string? Gender);

    public sealed class FamilyMemberRequest
    {
        public string? Name { get; set; }
        public string? Relation { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
    }

    [HttpGet("family-members")]
    public async Task<ActionResult<ApiResponse<List<FamilyMemberDto>>>> Family() =>
        Ok(await db.FamilyMembers.AsNoTracking().Where(m => m.PatientId == UserId && !m.IsDeleted).OrderBy(m => m.CreatedAt)
            .Select(m => new FamilyMemberDto(m.Id, m.Name, m.Relation, m.Age, m.Gender)).ToListAsync());

    [HttpPost("family-members")]
    public async Task<ActionResult<ApiResponse<FamilyMemberDto>>> AddFamily(FamilyMemberRequest req)
    {
        ValidateMember(req);
        var m = new FamilyMember { Id = Guid.NewGuid(), PatientId = UserId, CreatedAt = DateTime.UtcNow };
        ApplyMember(m, req);
        db.FamilyMembers.Add(m);
        await db.SaveChangesAsync();
        return Ok(new FamilyMemberDto(m.Id, m.Name, m.Relation, m.Age, m.Gender));
    }

    [HttpPut("family-members/{id:guid}")]
    public async Task<ActionResult<ApiResponse<FamilyMemberDto>>> UpdateFamily(Guid id, FamilyMemberRequest req)
    {
        ValidateMember(req);
        var m = await db.FamilyMembers.FirstOrDefaultAsync(x => x.Id == id && x.PatientId == UserId && !x.IsDeleted)
                ?? throw ApiException.NotFound("Family member not found");
        ApplyMember(m, req);
        await db.SaveChangesAsync();
        return Ok(new FamilyMemberDto(m.Id, m.Name, m.Relation, m.Age, m.Gender));
    }

    [HttpDelete("family-members/{id:guid}")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> DeleteFamily(Guid id)
    {
        var m = await db.FamilyMembers.FirstOrDefaultAsync(x => x.Id == id && x.PatientId == UserId && !x.IsDeleted)
                ?? throw ApiException.NotFound("Family member not found");
        m.IsDeleted = true;
        await db.SaveChangesAsync();
        return Success();
    }

    private static void ValidateMember(FamilyMemberRequest r)
    {
        var e = new FieldErrors();
        e.RequireText("name", r.Name, 2, 100, "Enter the name");
        e.RequireText("relation", r.Relation, 2, 30, "Enter the relation, e.g. Mother");
        if (r.Age is < 0 or > 120) e.Add("age", ErrorCodes.InvalidValue, "Enter a valid age");
        if (r.Gender != null && !Genders.Contains(r.Gender)) e.Add("gender", ErrorCodes.InvalidValue, "Gender must be Male, Female or Other");
        e.ThrowIfAny();
    }

    private static void ApplyMember(FamilyMember m, FamilyMemberRequest r)
    {
        m.Name = r.Name!.Trim();
        m.Relation = r.Relation!.Trim();
        m.Age = (short?)r.Age;
        m.Gender = r.Gender;
    }

    // ------------------------------------------------------------ devices

    [HttpPost("devices")]
    public async Task<ActionResult<ApiResponse<SuccessResult>>> Device(DeviceTokenRequest req)
    {
        await devices.RegisterAsync(UserTypes.Patient, UserId, req.PushToken, req.Platform);
        return Success();
    }
}
