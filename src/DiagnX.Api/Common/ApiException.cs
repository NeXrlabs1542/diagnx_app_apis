namespace DiagnX.Api.Common;

/// <summary>
/// Thrown anywhere in a request to produce the standard error envelope. The exception
/// middleware turns it into <see cref="ApiErrorResponse"/> with the given HTTP status.
/// </summary>
public class ApiException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    public IReadOnlyList<FieldError>? Fields { get; init; }
    public int? FirstInvalidStep { get; init; }
    public object? Details { get; init; }
    public int? RetryAfterSeconds { get; init; }

    public ApiException(int statusCode, string code, string message) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public static ApiException NotFound(string message = "Not found") => new(404, ErrorCodes.NotFound, message);
    public static ApiException Forbidden(string message = "You are not allowed to do this") => new(403, ErrorCodes.Forbidden, message);
    public static ApiException Unauthorized(string message = "Please log in again") => new(401, ErrorCodes.Unauthorized, message);
    public static ApiException BadRequest(string message) => new(400, ErrorCodes.BadRequest, message);
    public static ApiException Conflict(string code, string message) => new(409, code, message);
    public static ApiException Unprocessable(string code, string message) => new(422, code, message);

    public static ApiException Validation(IReadOnlyList<FieldError> fields, string message = "Please correct the highlighted fields") =>
        new(422, ErrorCodes.ValidationFailed, message) { Fields = fields };

    public static ApiException Field(string field, string code, string message) =>
        Validation(new[] { new FieldError(field, code, message) }, message);
}

public static class ErrorCodes
{
    public const string BadRequest = "BAD_REQUEST";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string ServerError = "SERVER_ERROR";
    public const string RateLimited = "RATE_LIMITED";

    // Auth / OTP
    public const string InvalidPhone = "INVALID_PHONE";
    public const string OtpInvalid = "OTP_INVALID";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string OtpAttemptsExceeded = "OTP_ATTEMPTS_EXCEEDED";
    public const string AccountBlocked = "ACCOUNT_BLOCKED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";

    // KYC
    public const string ApplicationLocked = "APPLICATION_LOCKED";
    public const string AlreadySubmitted = "ALREADY_SUBMITTED";
    public const string IncompleteApplication = "INCOMPLETE_APPLICATION";
    public const string InvalidState = "INVALID_STATE";
    public const string DocTooLarge = "DOC_TOO_LARGE";
    public const string DocTypeUnsupported = "DOC_TYPE_UNSUPPORTED";
    public const string KycNotApproved = "KYC_NOT_APPROVED";
    public const string UpstreamUnavailable = "UPSTREAM_UNAVAILABLE";

    // Field-level codes (error.fields[].code)
    public const string Required = "REQUIRED";
    public const string InvalidPan = "INVALID_PAN";
    public const string InvalidGstin = "INVALID_GSTIN";
    public const string InvalidCin = "INVALID_CIN";
    public const string InvalidLlpin = "INVALID_LLPIN";
    public const string InvalidAadhaar = "INVALID_AADHAAR";
    public const string InvalidIfsc = "INVALID_IFSC";
    public const string InvalidDob = "INVALID_DOB";
    public const string LicenceExpired = "LICENCE_EXPIRED";
    public const string InvalidAccount = "INVALID_ACCOUNT";
    public const string InvalidPincode = "INVALID_PINCODE";
    public const string InvalidYear = "INVALID_YEAR";
    public const string InvalidHours = "INVALID_HOURS";
    public const string InvalidEmail = "INVALID_EMAIL";
    public const string InvalidWebsite = "INVALID_WEBSITE";
    public const string InvalidLocation = "INVALID_LOCATION";
    public const string InvalidValue = "INVALID_VALUE";
    public const string InvalidDate = "INVALID_DATE";
    public const string DocRequired = "DOC_REQUIRED";
    public const string DocWrongType = "DOC_WRONG_TYPE";
    public const string ConsentRequired = "CONSENT_REQUIRED";
    public const string Mismatch = "MISMATCH";
    public const string TooLong = "TOO_LONG";

    // Bookings
    public const string SlotUnavailable = "SLOT_UNAVAILABLE";
    public const string NotServiceable = "NOT_SERVICEABLE";
    public const string TestNotOffered = "TEST_NOT_OFFERED";
    public const string LabUnavailable = "LAB_UNAVAILABLE";
    public const string InvalidTransition = "INVALID_TRANSITION";
    public const string CollectionOtpInvalid = "COLLECTION_OTP_INVALID";
    public const string PaymentFailed = "PAYMENT_FAILED";
    public const string AlreadyReviewed = "ALREADY_REVIEWED";
    public const string ReportIncomplete = "REPORT_INCOMPLETE";
}
