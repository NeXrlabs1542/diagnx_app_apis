using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DiagnX.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "booking_number_seq",
                startValue: 100001L);

            migrationBuilder.CreateSequence(
                name: "kyc_reference_seq",
                startValue: 100001L);

            migrationBuilder.CreateSequence(
                name: "report_number_seq",
                startValue: 100001L);

            migrationBuilder.CreateTable(
                name: "admin_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_config",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_config", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    actor_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    actor_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    entity_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auth_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    device_platform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    app_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    push_token = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    platform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "health_tips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    icon = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    read_mins = table.Column<short>(type: "smallint", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_health_tips", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_options",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    hint = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_master_options", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_states",
                columns: table => new
                {
                    code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    gst_state_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_master_states", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "otp_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audience = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    phone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    otp_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attempts = table.Column<short>(type: "smallint", nullable: false),
                    resend_count = table.Column<short>(type: "smallint", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    device_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_otp_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "partners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    country_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    kyc_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "patients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    country_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    age = table.Column<short>(type: "smallint", nullable: true),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pincode_cache",
                columns: table => new
                {
                    pincode = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: false),
                    areas = table.Column<List<string>>(type: "text[]", nullable: false),
                    city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    state = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pincode_cache", x => x.pincode);
                });

            migrationBuilder.CreateTable(
                name: "promo_banners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    subtitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    icon = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    gradient_from = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    gradient_to = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    target_test_slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promo_banners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stored_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    owner_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "test_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    icon = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    show_on_home = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "time_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    period = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    home_collection = table.Column<bool>(type: "boolean", nullable: false),
                    walk_in = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_slots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kyc_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_step = table.Column<short>(type: "smallint", nullable: false),
                    completed_steps = table.Column<List<short>>(type: "smallint[]", nullable: false),
                    last_saved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expected_by = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "jsonb", nullable: true),
                    draft_sections = table.Column<string>(type: "jsonb", nullable: true),
                    submit_idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kyc_applications", x => x.id);
                    table.ForeignKey(
                        name: "fk_kyc_applications_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "family_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    relation = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    age = table.Column<short>(type: "smallint", nullable: true),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_family_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_family_members_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    line1 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    line2 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    landmark = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    state = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    pincode = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_addresses", x => x.id);
                    table.ForeignKey(
                        name: "fk_patient_addresses_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fasting_required = table.Column<bool>(type: "boolean", nullable: false),
                    sample_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_package = table.Column<bool>(type: "boolean", nullable: false),
                    included_tests = table.Column<List<string>>(type: "text[]", nullable: false),
                    parameter_count = table.Column<short>(type: "smallint", nullable: false),
                    is_popular = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tests", x => x.id);
                    table.ForeignKey(
                        name: "fk_tests_test_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "test_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kyc_declarations",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decl_true = table.Column<bool>(type: "boolean", nullable: false),
                    decl_verify = table.Column<bool>(type: "boolean", nullable: false),
                    decl_terms = table.Column<bool>(type: "boolean", nullable: false),
                    agreement_version = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    device_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kyc_declarations", x => x.application_id);
                    table.ForeignKey(
                        name: "fk_kyc_declarations_kyc_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "kyc_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kyc_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    stored_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    reject_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kyc_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_kyc_documents_kyc_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "kyc_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_kyc_documents_stored_files_stored_file_id",
                        column: x => x.stored_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kyc_signatories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    designation = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    dob = table.Column<DateOnly>(type: "date", nullable: false),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    pan_enc = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pan_masked = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    aadhaar_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    aadhaar_last4 = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: false),
                    aadhaar_consent = table.Column<bool>(type: "boolean", nullable: false),
                    aadhaar_consent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    aadhaar_consent_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    pan_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aadhaar_front_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aadhaar_back_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    selfie_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pan_verified = table.Column<bool>(type: "boolean", nullable: false),
                    aadhaar_verified = table.Column<bool>(type: "boolean", nullable: false),
                    name_match_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kyc_signatories", x => x.id);
                    table.ForeignKey(
                        name: "fk_kyc_signatories_kyc_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "kyc_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kyc_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    changed_by = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kyc_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_kyc_status_history_kyc_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "kyc_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kyc_verification_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    request_ref = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    response_json = table.Column<string>(type: "jsonb", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kyc_verification_checks", x => x.id);
                    table.ForeignKey(
                        name: "fk_kyc_verification_checks_kyc_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "kyc_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "labs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    legal_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    brand_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    business_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    established_year = table.Column<short>(type: "smallint", nullable: false),
                    address_line1 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    address_line2 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    landmark = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    state = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    pincode = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: false),
                    area = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    location_accuracy_m = table.Column<int>(type: "integer", nullable: true),
                    location_captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lab_phone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    lab_email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    website = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    has_nabl = table.Column<bool>(type: "boolean", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    turnaround_hours = table.Column<short>(type: "smallint", nullable: false),
                    walk_in = table.Column<bool>(type: "boolean", nullable: false),
                    iso_certified = table.Column<bool>(type: "boolean", nullable: false),
                    accepting_bookings = table.Column<bool>(type: "boolean", nullable: false),
                    slot_capacity = table.Column<short>(type: "smallint", nullable: false),
                    logo_color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    rating_avg = table.Column<decimal>(type: "numeric(2,1)", precision: 2, scale: 1, nullable: false),
                    rating_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_labs", x => x.id);
                    table.ForeignKey(
                        name: "fk_labs_kyc_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "kyc_applications",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_labs_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "test_category_links",
                columns: table => new
                {
                    test_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_category_links", x => new { x.test_id, x.category_id });
                    table.ForeignKey(
                        name: "fk_test_category_links_test_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "test_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_test_category_links_tests_test_id",
                        column: x => x.test_id,
                        principalTable: "tests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "test_parameters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference_range = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ref_low = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    ref_high = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    value_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_parameters", x => x.id);
                    table.ForeignKey(
                        name: "fk_test_parameters_tests_test_id",
                        column: x => x.test_id,
                        principalTable: "tests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_bank_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_holder = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_number_enc = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_last4 = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: false),
                    ifsc = table.Column<string>(type: "character(11)", fixedLength: true, maxLength: 11, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    branch_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    account_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    penny_drop_status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    penny_drop_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lab_bank_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_lab_bank_accounts_kyc_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "kyc_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lab_bank_accounts_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_licences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    licence_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    valid_upto = table.Column<DateOnly>(type: "date", nullable: true),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lab_licences", x => x.id);
                    table.ForeignKey(
                        name: "fk_lab_licences_kyc_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "kyc_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lab_licences_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_medical_directors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    qualification = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    council_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    registration_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lab_medical_directors", x => x.id);
                    table.ForeignKey(
                        name: "fk_lab_medical_directors_kyc_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "kyc_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lab_medical_directors_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_operations",
                columns: table => new
                {
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processing_mode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    home_collection = table.Column<bool>(type: "boolean", nullable: false),
                    phlebotomist_count = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    open_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    close_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    front_photo_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    interior_photo_doc_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lab_operations", x => x.lab_id);
                    table.ForeignKey(
                        name: "fk_lab_operations_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_serviceable_pincodes",
                columns: table => new
                {
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pincode = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lab_serviceable_pincodes", x => new { x.lab_id, x.pincode });
                    table.ForeignKey(
                        name: "fk_lab_serviceable_pincodes_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_services",
                columns: table => new
                {
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lab_services", x => new { x.lab_id, x.service_code });
                    table.ForeignKey(
                        name: "fk_lab_services_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_tax_details",
                columns: table => new
                {
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_pan = table.Column<string>(type: "character(10)", fixedLength: true, maxLength: 10, nullable: false),
                    gstin = table.Column<string>(type: "character(15)", fixedLength: true, maxLength: 15, nullable: true),
                    pan_verified = table.Column<bool>(type: "boolean", nullable: false),
                    gstin_verified = table.Column<bool>(type: "boolean", nullable: false),
                    registered_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lab_tax_details", x => x.lab_id);
                    table.ForeignKey(
                        name: "fk_lab_tax_details_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_tests",
                columns: table => new
                {
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mrp = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lab_tests", x => new { x.lab_id, x.test_id });
                    table.ForeignKey(
                        name: "fk_lab_tests_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lab_tests_tests_test_id",
                        column: x => x.test_id,
                        principalTable: "tests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_working_days",
                columns: table => new
                {
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lab_working_days", x => new { x.lab_id, x.day_code });
                    table.ForeignKey(
                        name: "fk_lab_working_days_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "phlebotomists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phlebotomists", x => x.id);
                    table.ForeignKey(
                        name: "fk_phlebotomists_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    patient_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    patient_age = table.Column<short>(type: "smallint", nullable: true),
                    patient_gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lab_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    test_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    address_id = table.Column<Guid>(type: "uuid", nullable: true),
                    address_label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    address_text = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    address_pincode = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: true),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    slot_start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    mrp = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    discount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    payment_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    collection_otp = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    phlebotomist_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cancelled_by = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    collected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    report_ready_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.ForeignKey(
                        name: "fk_bookings_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_bookings_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_phlebotomists_phlebotomist_id",
                        column: x => x.phlebotomist_id,
                        principalTable: "phlebotomists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_bookings_tests_test_id",
                        column: x => x.test_id,
                        principalTable: "tests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_status_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    actor_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_status_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_status_events_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    gateway = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gateway_order_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    gateway_payment_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refunded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    pathologist_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pathologist_reg_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    has_flags = table.Column<bool>(type: "boolean", nullable: false),
                    attachment_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_reports_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reports_stored_files_attachment_file_id",
                        column: x => x.attachment_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phlebotomist_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lab_rating = table.Column<short>(type: "smallint", nullable: false),
                    phlebo_rating = table.Column<short>(type: "smallint", nullable: true),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_reviews_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviews_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    result = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference_range = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    flag = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_values", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_values_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_email",
                table: "admin_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created_at",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_auth_sessions_refresh_token_hash",
                table: "auth_sessions",
                column: "refresh_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auth_sessions_user_type_user_id",
                table: "auth_sessions",
                columns: new[] { "user_type", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_status_events_booking_id",
                table: "booking_status_events",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_booking_number",
                table: "bookings",
                column: "booking_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookings_family_member_id",
                table: "bookings",
                column: "family_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_lab_id_scheduled_date_slot_id",
                table: "bookings",
                columns: new[] { "lab_id", "scheduled_date", "slot_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_patient_id_created_at",
                table: "bookings",
                columns: new[] { "patient_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_patient_id_idempotency_key",
                table: "bookings",
                columns: new[] { "patient_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_phlebotomist_id",
                table: "bookings",
                column: "phlebotomist_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_status",
                table: "bookings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_test_id",
                table: "bookings",
                column: "test_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_tokens_push_token",
                table: "device_tokens",
                column: "push_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_tokens_user_type_user_id",
                table: "device_tokens",
                columns: new[] { "user_type", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_family_members_patient_id",
                table: "family_members",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_kyc_applications_partner_id",
                table: "kyc_applications",
                column: "partner_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kyc_applications_reference_id",
                table: "kyc_applications",
                column: "reference_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kyc_applications_status",
                table: "kyc_applications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_kyc_documents_application_id_doc_type",
                table: "kyc_documents",
                columns: new[] { "application_id", "doc_type" });

            migrationBuilder.CreateIndex(
                name: "ix_kyc_documents_stored_file_id",
                table: "kyc_documents",
                column: "stored_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_kyc_signatories_application_id",
                table: "kyc_signatories",
                column: "application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kyc_status_history_application_id",
                table: "kyc_status_history",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_kyc_verification_checks_application_id_check_type",
                table: "kyc_verification_checks",
                columns: new[] { "application_id", "check_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lab_bank_accounts_document_id",
                table: "lab_bank_accounts",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_lab_bank_accounts_lab_id",
                table: "lab_bank_accounts",
                column: "lab_id");

            migrationBuilder.CreateIndex(
                name: "ix_lab_licences_document_id",
                table: "lab_licences",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_lab_licences_lab_id_licence_type",
                table: "lab_licences",
                columns: new[] { "lab_id", "licence_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lab_medical_directors_document_id",
                table: "lab_medical_directors",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_lab_medical_directors_lab_id",
                table: "lab_medical_directors",
                column: "lab_id");

            migrationBuilder.CreateIndex(
                name: "ix_lab_serviceable_pincodes_pincode",
                table: "lab_serviceable_pincodes",
                column: "pincode");

            migrationBuilder.CreateIndex(
                name: "ix_lab_tax_details_business_pan",
                table: "lab_tax_details",
                column: "business_pan");

            migrationBuilder.CreateIndex(
                name: "ix_lab_tests_test_id",
                table: "lab_tests",
                column: "test_id");

            migrationBuilder.CreateIndex(
                name: "ix_labs_application_id",
                table: "labs",
                column: "application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_labs_city",
                table: "labs",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "ix_labs_is_active",
                table: "labs",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_labs_latitude_longitude",
                table: "labs",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_labs_partner_id",
                table: "labs",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_labs_pincode",
                table: "labs",
                column: "pincode");

            migrationBuilder.CreateIndex(
                name: "ix_master_options_group_code_code",
                table: "master_options",
                columns: new[] { "group_code", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_states_name",
                table: "master_states",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_type_user_id_created_at",
                table: "notifications",
                columns: new[] { "user_type", "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_otp_requests_ip_address_created_at",
                table: "otp_requests",
                columns: new[] { "ip_address", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_otp_requests_phone_audience_created_at",
                table: "otp_requests",
                columns: new[] { "phone", "audience", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_partners_kyc_status",
                table: "partners",
                column: "kyc_status");

            migrationBuilder.CreateIndex(
                name: "ix_partners_phone",
                table: "partners",
                column: "phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_addresses_patient_id",
                table: "patient_addresses",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_patients_phone",
                table: "patients",
                column: "phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_booking_id",
                table: "payments",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_gateway_order_id",
                table: "payments",
                column: "gateway_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_phlebotomists_lab_id",
                table: "phlebotomists",
                column: "lab_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_values_report_id",
                table: "report_values",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_attachment_file_id",
                table: "reports",
                column: "attachment_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_booking_id",
                table: "reports",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reports_report_number",
                table: "reports",
                column: "report_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_booking_id",
                table: "reviews",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_lab_id_created_at",
                table: "reviews",
                columns: new[] { "lab_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_patient_id",
                table: "reviews",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_test_categories_slug",
                table: "test_categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_test_category_links_category_id",
                table: "test_category_links",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_test_parameters_test_id",
                table: "test_parameters",
                column: "test_id");

            migrationBuilder.CreateIndex(
                name: "ix_tests_category_id",
                table: "tests",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_tests_slug",
                table: "tests",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_users");

            migrationBuilder.DropTable(
                name: "app_config");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "auth_sessions");

            migrationBuilder.DropTable(
                name: "booking_status_events");

            migrationBuilder.DropTable(
                name: "device_tokens");

            migrationBuilder.DropTable(
                name: "health_tips");

            migrationBuilder.DropTable(
                name: "kyc_declarations");

            migrationBuilder.DropTable(
                name: "kyc_signatories");

            migrationBuilder.DropTable(
                name: "kyc_status_history");

            migrationBuilder.DropTable(
                name: "kyc_verification_checks");

            migrationBuilder.DropTable(
                name: "lab_bank_accounts");

            migrationBuilder.DropTable(
                name: "lab_licences");

            migrationBuilder.DropTable(
                name: "lab_medical_directors");

            migrationBuilder.DropTable(
                name: "lab_operations");

            migrationBuilder.DropTable(
                name: "lab_serviceable_pincodes");

            migrationBuilder.DropTable(
                name: "lab_services");

            migrationBuilder.DropTable(
                name: "lab_tax_details");

            migrationBuilder.DropTable(
                name: "lab_tests");

            migrationBuilder.DropTable(
                name: "lab_working_days");

            migrationBuilder.DropTable(
                name: "master_options");

            migrationBuilder.DropTable(
                name: "master_states");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "otp_requests");

            migrationBuilder.DropTable(
                name: "patient_addresses");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "pincode_cache");

            migrationBuilder.DropTable(
                name: "promo_banners");

            migrationBuilder.DropTable(
                name: "report_values");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "test_category_links");

            migrationBuilder.DropTable(
                name: "test_parameters");

            migrationBuilder.DropTable(
                name: "time_slots");

            migrationBuilder.DropTable(
                name: "kyc_documents");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "stored_files");

            migrationBuilder.DropTable(
                name: "family_members");

            migrationBuilder.DropTable(
                name: "phlebotomists");

            migrationBuilder.DropTable(
                name: "tests");

            migrationBuilder.DropTable(
                name: "patients");

            migrationBuilder.DropTable(
                name: "labs");

            migrationBuilder.DropTable(
                name: "test_categories");

            migrationBuilder.DropTable(
                name: "kyc_applications");

            migrationBuilder.DropTable(
                name: "partners");

            migrationBuilder.DropSequence(
                name: "booking_number_seq");

            migrationBuilder.DropSequence(
                name: "kyc_reference_seq");

            migrationBuilder.DropSequence(
                name: "report_number_seq");
        }
    }
}
