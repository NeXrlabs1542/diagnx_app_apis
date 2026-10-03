"""
End-to-end smoke test for the DiagnX API. Walks both real journeys against a running server:

  A. Patient books a test -> pays -> lab collects the sample -> enters results -> patient reads
     the standardized report and rates the lab.
  B. A new lab registers -> completes the 6-step KYC -> admin approves -> lab sets a price ->
     the lab shows up in the patient's "compare labs" list.

Usage:  python tests/e2e/smoke.py [base_url]        (default http://127.0.0.1:5118)
Needs Otp:DevMode=true (fixed OTPs) and the seeded demo data. Uses only the standard library.
"""
import json
import time
import random
import sys
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timedelta, timezone

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5118").rstrip("/") + "/api/v1"
ADMIN_EMAIL = sys.argv[2] if len(sys.argv) > 2 else "admin@diagnx.local"
ADMIN_PASSWORD = sys.argv[3] if len(sys.argv) > 3 else "Admin@12345"
IST = timezone(timedelta(hours=5, minutes=30))

passed = failed = 0


def call(method, path, body=None, token=None, headers=None, files=None):
    """Returns (status, parsed_json). `files` = {field: (filename, bytes)} + body as form fields."""
    h = {"Accept": "application/json", "X-Device-Id": "e2e-test"}
    if token:
        h["Authorization"] = "Bearer " + token
    h.update(headers or {})
    data = None
    if files is not None:
        boundary = uuid.uuid4().hex
        parts = []
        for k, v in (body or {}).items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
        for k, (fname, content) in files.items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"; filename="{fname}"\r\n'
                         f'Content-Type: application/octet-stream\r\n\r\n'.encode() + content + b"\r\n")
        data = b"".join(parts) + f"--{boundary}--\r\n".encode()
        h["Content-Type"] = "multipart/form-data; boundary=" + boundary
    elif body is not None:
        data = json.dumps(body).encode()
        h["Content-Type"] = "application/json"
    req = urllib.request.Request(BASE + path, data=data, method=method, headers=h)
    started = time.time()
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, json.loads(r.read() or b"{}")
    except urllib.error.HTTPError as e:
        raw = e.read()
        try:
            return e.code, json.loads(raw)
        except ValueError:
            return e.code, {"raw": raw.decode(errors="replace")[:300]}
    finally:
        if time.time() - started > 3:
            print(f"  SLOW  {method} {path} took {time.time() - started:.1f}s")


def check(name, cond, detail=""):
    global passed, failed
    if cond:
        passed += 1
        print(f"  PASS  {name}")
    else:
        failed += 1
        print(f"  FAIL  {name}   {detail}")
    return cond


def ok(name, res, expect=200):
    status, body = res
    good = status == expect and (expect != 200 or body.get("success") is True)
    check(name, good, f"-> HTTP {status} {json.dumps(body)[:400]}")
    return body.get("data") if good and expect == 200 else body


def err(name, res, status, code):
    s, body = res
    got = (body.get("error") or {}).get("code")
    check(f"{name} -> {status} {code}", s == status and got == code, f"-> HTTP {s} {json.dumps(body)[:300]}")
    return body.get("error") or {}


def login(kind, phone, otp):
    sent = ok(f"{kind}: send OTP", call("POST", f"/{kind}/auth/otp/send", {"phone": phone}))
    data = ok(f"{kind}: verify OTP", call("POST", f"/{kind}/auth/otp/verify", {"otpRequestId": sent["otpRequestId"], "otp": otp}))
    return data


def section(title):
    print(f"\n=== {title} ===")


JPEG = bytes([0xFF, 0xD8, 0xFF, 0xE0]) + b"\x00\x10JFIF" + b"\x00" * 200 + bytes([0xFF, 0xD9])
PDF = b"%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"

# --------------------------------------------------------------------------- shared
section("Health + master data")
with urllib.request.urlopen(BASE.replace("/api/v1", "") + "/health", timeout=10) as r:
    check("health check", json.loads(r.read())["database"] is True)
cfg = ok("MST-02 config", call("GET", "/master/config"))
check("config: partner OTP 6 digits, patient 4", cfg["otpLength"] == 6 and cfg["patientOtpLength"] == 4)
opts = ok("MST-01 options", call("GET", "/master/options"))
check("options: 36 states, 6 business types, 38 time slots",
      len(opts["STATE"]) == 36 and len(opts["BUSINESS_TYPE"]) == 6 and len(opts["TIME_SLOT"]) == 38, str({k: len(v) for k, v in opts.items()}))
err("protected endpoint without token", call("GET", "/patient/profile"), 401, "UNAUTHORIZED")

# --------------------------------------------------------------------------- A. patient journey
section("A1. Patient login + profile")
patient_phone = "98765" + str(random.randint(10000, 99999))
err("invalid phone", call("POST", "/patient/auth/otp/send", {"phone": "12345"}), 422, "VALIDATION_FAILED")
sent = ok("patient: send OTP", call("POST", "/patient/auth/otp/send", {"phone": patient_phone}))
e = err("wrong OTP", call("POST", "/patient/auth/otp/verify", {"otpRequestId": sent["otpRequestId"], "otp": "0000"}), 401, "OTP_INVALID")
check("wrong OTP reports attemptsLeft", (e.get("details") or {}).get("attemptsLeft") == 4, str(e))
auth = ok("patient: verify OTP", call("POST", "/patient/auth/otp/verify", {"otpRequestId": sent["otpRequestId"], "otp": "1234"}))
P = auth["accessToken"]
check("new patient flagged", auth["isNewUser"] is True)
refreshed = ok("refresh token", call("POST", "/patient/auth/token/refresh", {"refreshToken": auth["refreshToken"]}))
err("old refresh token no longer works", call("POST", "/patient/auth/token/refresh", {"refreshToken": auth["refreshToken"]}), 401, "UNAUTHORIZED")
P = refreshed["accessToken"]
err("partner endpoint with patient token", call("GET", "/partner/me", token=P), 403, "FORBIDDEN")
ok("update profile", call("PUT", "/patient/profile", {"name": "Vamshi Krishna", "email": "vamshi@example.com", "age": 29, "gender": "Male", "city": "Hyderabad"}, P))

section("A2. Browse, search, compare")
home = ok("home", call("GET", "/patient/home?lat=17.4600&lng=78.3640", token=P))
check("home has categories, popular tests, packages, labs, banners, tips",
      len(home["categories"]) == 6 and len(home["popularTests"]) >= 4 and len(home["packages"]) == 2 and len(home["topLabs"]) == 5
      and len(home["promoBanners"]) == 3 and len(home["healthTips"]) == 3, str({k: (len(v) if isinstance(v, list) else v) for k, v in home.items()}))
check("top labs sorted by rating, with distance", home["topLabs"][0]["name"] == "Vitalis Diagnostics" and home["topLabs"][0]["distanceKm"] is not None)
found = ok("search 'thyroid'", call("GET", "/patient/tests?q=thyroid"))
check("search finds thyroid profile + packages that include it", any(t["slug"] == "test-thyroid" for t in found) and len(found) >= 2, str([t["slug"] for t in found]))
wh = ok("category: women's health", call("GET", "/patient/tests?category=womens-health"))
check("women's health category has tests", len(wh) >= 3, str([t["slug"] for t in wh]))
detail = ok("test detail (CBC)", call("GET", "/patient/tests/test-cbc"))
check("CBC: 5 standard report parameters, 5 labs, from price", len(detail["reportParameters"]) == 5 and detail["test"]["labCount"] >= 5 and detail["test"]["fromPrice"] <= 289)
offers = ok("compare labs for CBC (by price)", call("GET", "/patient/tests/test-cbc/labs?sort=price&lat=17.46&lng=78.364&pincode=500084"))
prices = [o["price"] for o in offers]
check("offers from every lab, sorted by price", len(offers) >= 5 and prices == sorted(prices), str(prices))
check("Accura has no home collection", next(o for o in offers if o["lab"]["name"] == "Accura Diagnostics")["homeCollectionAvailable"] is False)
by_dist = ok("compare labs (by distance)", call("GET", "/patient/tests/test-cbc/labs?sort=distance&lat=17.46&lng=78.364"))
check("nearest first", by_dist[0]["lab"]["name"] == "Vitalis Diagnostics" and by_dist[0]["lab"]["distanceKm"] == 0)
vitalis = next(o for o in offers if o["lab"]["name"] == "Vitalis Diagnostics")
lab_id = vitalis["lab"]["id"]
lab = ok("lab profile", call("GET", f"/patient/labs/{lab_id}"))
check("lab profile: NABL + ISO, 10 tests, hours", lab["lab"]["accreditation"] == ["NABL", "ISO"] and len(lab["tests"]) == 10 and lab["openTime"] == "06:00")

section("A3. Address, slots, booking, payment")
addr = ok("add address", call("POST", "/patient/addresses", {"label": "Home", "line1": "Flat 302, Sri Sai Residency", "line2": "Kondapur Main Road", "city": "Hyderabad", "pincode": "500084"}, P))
check("first address is default", addr["isDefault"] is True)
far = ok("add far-away address", call("POST", "/patient/addresses", {"label": "Parents", "line1": "12 MG Road", "city": "Bengaluru", "pincode": "560001"}, P))
tomorrow = (datetime.now(IST) + timedelta(days=1)).strftime("%Y-%m-%d")
slots = ok("slots for tomorrow", call("GET", f"/patient/labs/{lab_id}/slots?date={tomorrow}&mode=home"))
free = [s for s in slots["slots"] if s["available"]]
check("9 slots, free ones available tomorrow", len(slots["slots"]) == 9 and len(free) >= 2, str(len(free)))
today_slots = ok("slots for today", call("GET", f"/patient/labs/{lab_id}/slots?date={datetime.now(IST).strftime('%Y-%m-%d')}&mode=home"))
check("past slots today are unavailable", all(not s["available"] for s in today_slots["slots"] if s["startTime"] <= datetime.now(IST).strftime("%H:%M")))

base_booking = {"labId": lab_id, "testId": "test-cbc", "mode": "home", "addressId": addr["id"], "date": tomorrow, "slotId": free[0]["id"], "paymentMethod": "upi"}
err("booking outside service area", call("POST", "/patient/bookings", {**base_booking, "addressId": far["id"]}, P), 422, "VALIDATION_FAILED")
err("booking in the past", call("POST", "/patient/bookings", {**base_booking, "date": "2020-01-01"}, P), 422, "VALIDATION_FAILED")
idem = str(uuid.uuid4())
created = ok("create booking (UPI)", call("POST", "/patient/bookings", base_booking, P, {"Idempotency-Key": idem}))
booking = created["booking"]
check("booking awaits payment with a payment intent", booking["status"] == "payment_pending" and created["nextAction"] == "PAY" and created["payment"]["amount"] == 349)
check("price snapshot: mrp 469, discount 120", booking["mrp"] == 469 and booking["discount"] == 120, str(booking["mrp"]))
again = ok("same Idempotency-Key returns same booking", call("POST", "/patient/bookings", base_booking, P, {"Idempotency-Key": idem}))
check("no double booking", again["booking"]["id"] == booking["id"])
err("declined payment", call("POST", f"/patient/payments/{created['payment']['paymentId']}/verify", {"gatewayPaymentId": "fail"}, P), 402, "PAYMENT_FAILED")
retry = ok("retry payment", call("POST", f"/patient/bookings/{booking['id']}/payment", token=P))
paid = ok("verify payment", call("POST", f"/patient/payments/{retry['paymentId']}/verify", {"gatewayPaymentId": "pay_test_1"}, P))
check("booking confirmed + paid, OTP visible", paid["status"] == "confirmed" and paid["paymentStatus"] == "PAID" and len(paid["otp"]) == 4)
B = booking["id"]

cod = ok("create COD booking", call("POST", "/patient/bookings", {**base_booking, "slotId": free[1]["id"], "paymentMethod": "cod"}, P))
check("COD confirmed immediately", cod["booking"]["status"] == "confirmed" and cod["nextAction"] == "NONE" and cod["booking"]["paymentStatus"] == "PAY_ON_COLLECTION")
cancelled = ok("cancel COD booking", call("POST", f"/patient/bookings/{cod['booking']['id']}/cancel", {"reason": "Booked by mistake"}, P))
check("cancelled", cancelled["status"] == "cancelled" and cancelled["canCancel"] is False)
active = ok("orders: active tab", call("GET", "/patient/bookings?tab=active", token=P))
check("active tab has only the paid booking", [b["id"] for b in active] == [B], str(len(active)))

section("A4. Lab side: assign, collect, results, publish (demo partner)")
lab_auth = login("partner", "9000000001", "123456")
L = lab_auth["accessToken"]
check("demo partner is APPROVED", lab_auth["partner"]["kycStatus"] == "APPROVED")
me = ok("PRT-01 me", call("GET", "/partner/me", token=L))
check("partner owns the live lab", me["labIsLive"] is True and me["labName"] == "Vitalis Diagnostics")
dash = ok("dashboard", call("GET", "/partner/dashboard", token=L))
check("dashboard shows the new order", dash["awaitingCollection"] >= 1 and dash["enrolledTests"] == 10 and dash["earnings30Days"] >= 349, str(dash["awaitingCollection"]))
orders = ok("orders list", call("GET", "/partner/bookings?status=confirmed", token=L))
mine = next(o for o in orders["items"] if o["id"] == B)
check("order shows patient + next actions", mine["patientName"] == "Vamshi Krishna" and "assign" in mine["nextActions"], str(mine["nextActions"]))
err("en route before assigning", call("POST", f"/partner/bookings/{B}/enroute", token=L), 422, "INVALID_TRANSITION")
phlebos = ok("phlebotomists", call("GET", "/partner/phlebotomists", token=L))
ok("assign phlebotomist", call("POST", f"/partner/bookings/{B}/assign", {"phlebotomistId": phlebos[0]["id"]}, L))
ok("mark en route", call("POST", f"/partner/bookings/{B}/enroute", token=L))
tracking = ok("patient tracking", call("GET", f"/patient/bookings/{B}", token=P))
check("patient sees phlebotomist + en-route step active",
      tracking["status"] == "enroute" and tracking["phlebotomistName"] == phlebos[0]["name"]
      and [s["state"] for s in tracking["timeline"]] == ["DONE", "ACTIVE", "TODO", "TODO", "TODO"], str([s["state"] for s in tracking["timeline"]]))
err("patient can't cancel once en route", call("POST", f"/patient/bookings/{B}/cancel", {}, P), 422, "INVALID_TRANSITION")
err("collect with wrong OTP", call("POST", f"/partner/bookings/{B}/collect", {"otp": "0000" if tracking["otp"] != "0000" else "1111"}, L), 422, "VALIDATION_FAILED")
ok("collect with patient's OTP", call("POST", f"/partner/bookings/{B}/collect", {"otp": tracking["otp"]}, L))
editor = ok("result-entry form", call("GET", f"/partner/bookings/{B}/report", token=L))
check("form has the 5 standard CBC parameters + default pathologist", len(editor["parameters"]) == 5 and editor["pathologistName"] == "Dr. Ramesh Rao")
values = {"Hemoglobin": "14.2", "RBC Count": "5.1", "WBC Count": "11.8", "Platelet Count": "2.4", "Hematocrit (PCV)": "38.5"}
partial = [{"parameterId": p["parameterId"], "result": values[p["name"]]} for p in editor["parameters"][:3]]
ok("save partial results", call("PUT", f"/partner/bookings/{B}/report", {"values": partial}, L))
e = err("publish with missing results", call("POST", f"/partner/bookings/{B}/report/publish", token=L), 422, "REPORT_INCOMPLETE")
check("lists the 2 missing parameters", len(e.get("fields") or []) == 2, str(e.get("fields")))
err("non-numeric result rejected", call("PUT", f"/partner/bookings/{B}/report", {"values": [{"parameterId": editor["parameters"][0]["parameterId"], "result": "abc"}]}, L), 422, "VALIDATION_FAILED")
full = [{"parameterId": p["parameterId"], "result": values[p["name"]]} for p in editor["parameters"]]
saved = ok("save all results", call("PUT", f"/partner/bookings/{B}/report", {"values": full, "remarks": "Mild leukocytosis."}, L))
flags = {p["name"]: p["flag"] for p in saved["parameters"]}
check("flags computed: WBC high, Hematocrit low, rest normal",
      flags == {"Hemoglobin": "normal", "RBC Count": "normal", "WBC Count": "high", "Platelet Count": "normal", "Hematocrit (PCV)": "low"}, str(flags))
ok("attach lab's original PDF", call("POST", f"/partner/bookings/{B}/report/attachment", {}, L, files={"file": ("report.pdf", PDF)}))
published = ok("publish report", call("POST", f"/partner/bookings/{B}/report/publish", token=L))
check("booking is ready", published["status"] == "ready" and published["reportPublished"] is True)

section("A5. Patient: report, review, notifications")
report = ok("standardized report", call("GET", f"/patient/bookings/{B}/report", token=P))
check("report: 5 parameters, flagged, pathologist, PDF link",
      len(report["parameters"]) == 5 and report["hasFlags"] is True and report["pathologist"] == "Dr. Ramesh Rao"
      and report["reportId"].startswith("RPT-") and report["labReportPdfUrl"], str(report.get("reportId")))
with urllib.request.urlopen(report["labReportPdfUrl"], timeout=10) as r:
    check("signed PDF link downloads", r.read() == PDF)
try:
    urllib.request.urlopen(report["labReportPdfUrl"].replace("sig=", "sig=0"), timeout=10)
    check("tampered file link rejected", False)
except urllib.error.HTTPError as ex:
    check("tampered file link rejected", ex.code == 403)
reports = ok("reports list", call("GET", "/patient/reports", token=P))
check("1 report with 2 flags", len(reports) == 1 and reports[0]["flagCount"] == 2)
other = login("patient", "98764" + str(random.randint(10000, 99999)), "1234")
err("another patient can't read this report", call("GET", f"/patient/bookings/{B}/report", token=other["accessToken"]), 404, "NOT_FOUND")
before = ok("lab before review", call("GET", f"/patient/labs/{lab_id}"))["lab"]
reviewed = ok("rate lab + phlebotomist", call("POST", f"/patient/bookings/{B}/review", {"labRating": 5, "phleboRating": 4, "comment": "On time and painless."}, P))
check("booking completed", reviewed["status"] == "completed" and reviewed["review"]["labRating"] == 5)
err("second review", call("POST", f"/patient/bookings/{B}/review", {"labRating": 1, "phleboRating": 1}, P), 409, "ALREADY_REVIEWED")
after = ok("lab after review", call("GET", f"/patient/labs/{lab_id}"))
check("review count +1 and review listed", after["lab"]["reviewCount"] == before["reviewCount"] + 1 and after["recentReviews"][0]["patientName"] == "Vamshi K.")
notes = ok("notifications", call("GET", "/patient/notifications", token=P))
kinds = [n["kind"] for n in notes["items"]]
check("notified at each step", all(k in kinds for k in ["booking", "enroute", "collected", "ready"]) and notes["unreadCount"] == len(kinds), str(kinds))
ok("mark all read", call("POST", "/patient/notifications/read-all", token=P))
check("unread is 0", ok("unread count", call("GET", "/patient/notifications/unread-count", token=P))["unreadCount"] == 0)

# --------------------------------------------------------------------------- B. partner onboarding
section("B1. New lab: login + KYC steps 1-5")
partner_phone = "91234" + str(random.randint(10000, 99999))
new = login("partner", partner_phone, "123456")
K = new["accessToken"]
check("new partner NOT_STARTED", new["partner"]["isNewPartner"] is True and new["partner"]["kycStatus"] == "NOT_STARTED" and new["partner"]["kycCurrentStep"] is None)
err("lab dashboard locked before approval", call("GET", "/partner/dashboard", token=K), 403, "KYC_NOT_APPROVED")
prog = ok("KYC-01 progress", call("GET", "/partner/kyc/progress", token=K))
check("no progress yet", prog["hasProgress"] is False and prog["status"] == "DRAFT")

profile = {"labLegalName": "Sri Sai Diagnostics Pvt Ltd", "labBrandName": "Sai Diagnostics", "businessType": "pvt_ltd", "establishedYear": 2015,
           "services": ["pathology"], "addressLine1": "12-4-56, Road No. 3, Banjara Hills", "city": "Hyderabad", "state": "Telangana",
           "pincode": "500034", "area": "Banjara Hills", "latitude": 17.423912, "longitude": 78.473802, "locationAccuracy": 12,
           "labPhone": "4023456789", "labEmail": "reports@saisdiagnostics.in", "website": "www.saisdiagnostics.in"}
e = err("step 1 with bad data", call("PUT", "/partner/kyc/application/profile", {**profile, "pincode": "050034", "latitude": 51.5, "longitude": -0.12, "labEmail": "x"}, K), 422, "VALIDATION_FAILED")
check("field errors name the inputs", {f["field"] for f in e["fields"]} == {"pincode", "latitude", "labEmail"}, str(e.get("fields")))
ok("X-Draft autosave of a partial step", call("PUT", "/partner/kyc/application/profile", {"labLegalName": "Sri Sai"}, K, {"X-Draft": "true"}))
draft = ok("KYC-02 application (draft)", call("GET", "/partner/kyc/application", token=K))
check("draft is returned, step not completed", draft["profile"]["labLegalName"] == "Sri Sai" and draft["completedSteps"] == [])
s1 = ok("KYC-03 step 1 profile", call("PUT", "/partner/kyc/application/profile", profile, K))
check("step 1 done, next is 2", s1["completedSteps"] == [1] and s1["nextStep"] == 2)
err("step 3 docs missing", call("PUT", "/partner/kyc/application/owner", {"ownerName": "Rajesh Kumar Sharma"}, K), 422, "VALIDATION_FAILED")

docs = {}
for doc_type in ["CLINICAL_EST", "BMW", "TRADE_LICENCE", "ENTITY_REG", "MEDICAL_REG", "PAN_CARD", "AADHAAR_FRONT", "AADHAAR_BACK",
                 "SELFIE", "CANCELLED_CHEQUE", "LAB_FRONT_PHOTO", "LAB_INTERIOR_PHOTO"]:
    photo = doc_type in ("SELFIE", "LAB_FRONT_PHOTO", "LAB_INTERIOR_PHOTO", "PAN_CARD")
    s, body = call("POST", "/partner/kyc/documents", {"docType": doc_type}, K, files={"file": ("f.jpg", JPEG) if photo else ("f.pdf", PDF)})
    if s == 200:
        docs[doc_type] = body["data"]["documentId"]
check("DOC-01 uploaded 12 documents", len(docs) == 12, str(len(docs)))
err("PDF not allowed for selfie", call("POST", "/partner/kyc/documents", {"docType": "SELFIE"}, K, files={"file": ("s.pdf", PDF)}), 415, "DOC_TYPE_UNSUPPORTED")
err("exe renamed to .jpg rejected", call("POST", "/partner/kyc/documents", {"docType": "PAN_CARD"}, K, files={"file": ("x.jpg", b"MZ" + b"\x00" * 100)}), 415, "DOC_TYPE_UNSUPPORTED")
err("file over 5 MB", call("POST", "/partner/kyc/documents", {"docType": "BMW"}, K, files={"file": ("big.pdf", PDF + b"0" * (5 * 1024 * 1024 + 10))}), 413, "DOC_TOO_LARGE")
url = ok("DOC-03 signed URL", call("GET", f"/partner/kyc/documents/{docs['PAN_CARD']}/url", token=K))
with urllib.request.urlopen(url["url"], timeout=10) as r:
    check("document downloads via signed URL", r.read() == JPEG)

licences = {"clinicalEstNumber": "TSCEA/HYD/2019/004521", "clinicalEstDocId": docs["CLINICAL_EST"], "bmwNumber": "TSPCB/BMW/2023/11876",
            "bmwValidUpto": "2030-12-31", "bmwDocId": docs["BMW"], "tradeLicenseNumber": "GHMC/SEA/2019/778812", "tradeLicenseDocId": docs["TRADE_LICENCE"],
            "businessPan": "AAACS1234C", "gstin": "36AAACS1234C1ZZ", "entityRegNumber": "U85110TG2015PTC123456", "entityRegDocId": docs["ENTITY_REG"],
            "hasNabl": False, "directorName": "Dr. Ramesh Rao", "directorQualification": "MD Pathology", "councilName": "Telangana Medical Council",
            "medicalRegNumber": "TSMC/45678", "medicalRegDocId": docs["MEDICAL_REG"]}
e = err("step 2 wrong PAN type + wrong doc", call("PUT", "/partner/kyc/application/licences", {**licences, "businessPan": "AAAPA1234C", "gstin": "", "bmwDocId": docs["PAN_CARD"]}, K), 422, "VALIDATION_FAILED")
codes = {f["field"]: f["code"] for f in e["fields"]}
check("PAN 4th-char rule + doc type rule", codes == {"businessPan": "INVALID_PAN", "bmwDocId": "DOC_WRONG_TYPE"}, str(codes))
ok("KYC-04 step 2 licences", call("PUT", "/partner/kyc/application/licences", licences, K))
owner = {"ownerName": "Rajesh Kumar Sharma", "ownerDesignation": "Director", "ownerDob": "1980-06-15", "ownerEmail": "rajesh@saisdiagnostics.in",
         "ownerPan": "ABCPS1234D", "aadhaarNumber": "234567890124", "aadhaarConsent": True, "panDocId": docs["PAN_CARD"],
         "aadhaarFrontDocId": docs["AADHAAR_FRONT"], "aadhaarBackDocId": docs["AADHAAR_BACK"], "selfieDocId": docs["SELFIE"]}
err("step 3 bad Aadhaar checksum", call("PUT", "/partner/kyc/application/owner", {**owner, "aadhaarNumber": "234567890123"}, K), 422, "VALIDATION_FAILED")
ok("KYC-05 step 3 owner", call("PUT", "/partner/kyc/application/owner", owner, K))
bank = {"accountHolder": "Sri Sai Diagnostics Pvt Ltd", "accountNumber": "123456789012", "confirmAccountNumber": "123456789012",
        "ifsc": "HDFC0001234", "bankName": "HDFC Bank", "branchName": "Banjara Hills", "accountType": "Current", "chequeDocId": docs["CANCELLED_CHEQUE"]}
ok("KYC-06 step 4 bank", call("PUT", "/partner/kyc/application/bank", bank, K))
ops = {"processingMode": "in_house", "homeCollection": True, "serviceablePincodes": ["500034", "500084"], "phlebotomistCount": "6–10",
       "workingDays": ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat"], "openTime": "07:00", "closeTime": "20:00",
       "frontPhotoDocId": docs["LAB_FRONT_PHOTO"], "interiorPhotoDocId": docs["LAB_INTERIOR_PHOTO"]}
s5 = ok("KYC-07 step 5 operations", call("PUT", "/partner/kyc/application/operations", ops, K))
check("all 5 steps complete, next is review", s5["completedSteps"] == [1, 2, 3, 4, 5] and s5["nextStep"] == 6)

section("B2. Review, submit, lock")
status, raw = call("GET", "/partner/kyc/application", token=K)
text = json.dumps(raw)
application = ok("KYC-02 application", (status, raw))
check("Aadhaar / account / PAN never returned in full", "234567890124" not in text and "123456789012" not in text and "ABCPS1234D" not in text)
check("masked values + provided flags", application["owner"]["aadhaarProvided"] is True and application["owner"]["aadhaarMasked"].endswith("0124")
      and application["bank"]["accountProvided"] is True and application["bank"]["accountMasked"].endswith("9012"), str(application["owner"]))
check("en-dash staff count normalised", application["operations"]["phlebotomistCount"] == "6-10")
ok("re-save owner without re-entering Aadhaar", call("PUT", "/partner/kyc/application/owner", {**owner, "aadhaarNumber": None}, K))
e = err("submit without accepting terms", call("POST", "/partner/kyc/application/submit", {"declTrue": True, "declVerify": True, "declTerms": False, "agreementVersion": "v1.0"}, K), 422, "INCOMPLETE_APPLICATION")
check("points to step 6", e.get("firstInvalidStep") == 6)
ok("delete a required document", call("DELETE", f"/partner/kyc/documents/{docs['CANCELLED_CHEQUE']}", token=K))
decl = {"declTrue": True, "declVerify": True, "declTerms": True, "agreementVersion": cfg["agreementVersion"]}
e = err("submit with a missing document", call("POST", "/partner/kyc/application/submit", decl, K), 422, "INCOMPLETE_APPLICATION")
check("points to step 4 / chequeDocId", e.get("firstInvalidStep") == 4 and e["fields"][0]["field"] == "chequeDocId", str(e))
cheque = ok("re-upload cheque", call("POST", "/partner/kyc/documents", {"docType": "CANCELLED_CHEQUE"}, K, files={"file": ("c.pdf", PDF)}))
ok("re-save step 4 with new document", call("PUT", "/partner/kyc/application/bank", {**bank, "accountNumber": None, "confirmAccountNumber": None, "chequeDocId": cheque["documentId"]}, K))
key = str(uuid.uuid4())
submitted = ok("KYC-08 submit", call("POST", "/partner/kyc/application/submit", decl, K, {"Idempotency-Key": key}))
check("reference id + PENDING", submitted["status"] == "PENDING" and submitted["referenceId"].startswith("DXP-"), str(submitted))
same = ok("submit retried with same Idempotency-Key", call("POST", "/partner/kyc/application/submit", decl, K, {"Idempotency-Key": key}))
check("same reference returned", same["referenceId"] == submitted["referenceId"])
err("submit again without key", call("POST", "/partner/kyc/application/submit", decl, K), 409, "ALREADY_SUBMITTED")
err("edit after submit", call("PUT", "/partner/kyc/application/profile", profile, K), 409, "APPLICATION_LOCKED")
err("upload after submit", call("POST", "/partner/kyc/documents", {"docType": "BMW"}, K, files={"file": ("f.pdf", PDF)}), 409, "APPLICATION_LOCKED")
st = ok("KYC-09 status", call("GET", "/partner/kyc/status", token=K))
check("timeline: submitted done, document review active", [s["state"] for s in st["timeline"]] == ["DONE", "ACTIVE", "TODO", "TODO"])
check("masked summary", st["summary"]["businessPan"].startswith("AAACS") and "1234" not in st["summary"]["businessPan"]
      and st["summary"]["documentCount"] == 12 and st["summary"]["coordinates"] == "17.423912, 78.473802", str(st["summary"]))
check("PRT-01 shows PENDING", ok("me", call("GET", "/partner/me", token=K))["kycStatus"] == "PENDING")

section("B3. Admin review + approval")
err("admin wrong password", call("POST", "/admin/auth/login", {"email": ADMIN_EMAIL, "password": "nope"}), 401, "INVALID_CREDENTIALS")
A = ok("admin login", call("POST", "/admin/auth/login", {"email": ADMIN_EMAIL, "password": ADMIN_PASSWORD}))["accessToken"]
err("partner token on admin API", call("GET", "/admin/stats", token=K), 403, "FORBIDDEN")
queue = ok("ADM-01 pending applications", call("GET", "/admin/kyc/applications?status=PENDING", token=A))
item = next(i for i in queue["items"] if i["referenceId"] == submitted["referenceId"])
check("application in the queue with 7 checks", item["labName"] == "Sri Sai Diagnostics Pvt Ltd" and item["checksTotal"] == 7, str(item))
app_id = item["applicationId"]
full_app = ok("ADM-02 full application", call("GET", f"/admin/kyc/applications/{app_id}", token=A))
check("reviewer sees unmasked values + 12 documents", full_app["owner"]["aadhaar"] == "234567890124" and full_app["bank"]["accountNumber"] == "123456789012" and len(full_app["documents"]) == 12)
ok("ADM-04 record a check result", call("POST", f"/admin/kyc/applications/{app_id}/verify", {"checkType": "PAN", "status": "PASSED"}, A))
st = ok("status after a check", call("GET", "/partner/kyc/status", token=K))
check("timeline moves to registry checks", [s["state"] for s in st["timeline"]] == ["DONE", "DONE", "ACTIVE", "TODO"])
err("request changes without reasons", call("POST", f"/admin/kyc/applications/{app_id}/decision", {"decision": "REQUEST_CHANGES"}, A), 422, "VALIDATION_FAILED")
ok("ADM-03 request changes", call("POST", f"/admin/kyc/applications/{app_id}/decision", {"decision": "REQUEST_CHANGES", "reasons": [{"field": "gstin", "message": "GSTIN not found in registry"}]}, A))
st = ok("partner sees the reason", call("GET", "/partner/kyc/status", token=K))
check("CHANGES_REQUESTED with reason", st["status"] == "CHANGES_REQUESTED" and st["rejectionReasons"][0]["field"] == "gstin")
ok("partner fixes step 2 (application unlocked)", call("PUT", "/partner/kyc/application/licences", {**licences, "gstin": ""}, K))
ok("partner resubmits", call("POST", "/partner/kyc/application/submit", decl, K))
ok("ADM-03 approve", call("POST", f"/admin/kyc/applications/{app_id}/decision", {"decision": "APPROVE", "note": "Verified by phone"}, A))
err("decide twice", call("POST", f"/admin/kyc/applications/{app_id}/decision", {"decision": "REJECT", "reasons": [{"field": "x", "message": "y"}]}, A), 409, "INVALID_STATE")
me = ok("partner me after approval", call("GET", "/partner/me", token=K))
check("APPROVED and lab is live", me["kycStatus"] == "APPROVED" and me["labIsLive"] is True)

section("B4. New lab goes to market")
catalog = ok("lab sees the master catalog", call("GET", "/partner/catalog", token=K))
check("10 catalog tests, none enrolled", len(catalog) == 10 and not any(c["enrolled"] for c in catalog))
err("price above MRP rejected", call("PUT", "/partner/tests/test-cbc", {"mrp": 200, "price": 300}, K), 422, "VALIDATION_FAILED")
ok("enrol CBC at Rs 199", call("PUT", "/partner/tests/test-cbc", {"mrp": 400, "price": 199}, K))
ok("add phlebotomist", call("POST", "/partner/phlebotomists", {"name": "Mahesh Yadav", "phone": "9000055566"}, K))
ok("lab settings", call("PATCH", "/partner/lab/settings", {"turnaroundHours": 10, "slotCapacity": 6}, K))
offers = ok("patient compares labs for CBC again", call("GET", "/patient/tests/test-cbc/labs?sort=price&pincode=500084"))
check("new lab is now the cheapest offer", len(offers) >= 6 and offers[0]["lab"]["name"] == "Sai Diagnostics" and offers[0]["price"] == 199
      and offers[0]["homeCollectionAvailable"] is True and offers[0]["lab"]["turnaroundHours"] == 10, str([(o["lab"]["name"], o["price"]) for o in offers][:2]))
stats = ok("admin stats", call("GET", "/admin/stats", token=A))
check("stats: new lab counted as live", stats["liveLabs"] >= 6 and stats["pendingKyc"] == 0, str(stats))
ok("partner logout", call("POST", "/partner/auth/logout", {}, K))

print(f"\n{'=' * 50}\n{passed} passed, {failed} failed")
sys.exit(1 if failed else 0)
