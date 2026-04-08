# Support Team Guide - Entra ID Migration

This guide helps support staff handle user issues during and after the Microsoft Entra ID migration.

---

## Table of Contents

1. [Quick Reference](#quick-reference)
2. [Pre-Migration Preparation](#pre-migration-preparation)
3. [Common Issues & Solutions](#common-issues--solutions)
4. [Escalation Procedures](#escalation-procedures)
5. [Testing User Accounts](#testing-user-accounts)
6. [Support Scripts](#support-scripts)

---

## Quick Reference

### Migration Overview

**What Changed:**
- Authentication provider: Keycloak → Microsoft Entra ID
- New feature: "Sign in with Microsoft" (in addition to Google)
- Backend: Same API, different auth provider
- Frontend: Updated login UI with MSAL library

**What Didn't Change:**
- User accounts (emails, passwords preserved)
- User data (gymnasts, sessions, programs)
- Database (only provider field renamed)
- Email/password login still works
- Google Sign-In still works

### Key Support Metrics

**Target Response Times:**
- Critical (cannot log in): < 15 minutes
- High (login issues): < 30 minutes
- Medium (feature questions): < 2 hours
- Low (general questions): < 24 hours

**Expected Ticket Volume:**
- Day 1: 50-100 tickets (spike expected)
- Days 2-3: 20-30 tickets/day
- Days 4-7: <10 tickets/day
- Week 2+: <5 tickets/day

**Common Issues (by frequency):**
1. "I can't log in" (40%)
2. "My session expires immediately" (20%)
3. "OAuth login doesn't work" (15%)
4. "Where's the Microsoft sign-in button?" (10%)
5. "I forgot my password" (10%)
6. Other (5%)

---

## Pre-Migration Preparation

### Training Checklist

**1 Week Before Migration:**
- [ ] Review this guide thoroughly
- [ ] Complete practice scenarios (see below)
- [ ] Familiarize yourself with Entra ID admin portal
- [ ] Join #migration-support Slack channel
- [ ] Confirm on-call schedule for migration weekend

**Practice Scenarios:**

**Scenario 1: Can't Log In**
- User: "I'm trying to log in but it says invalid credentials"
- Expected response: Verify email spelling, try password reset, check browser cookies
- Escalate if: Password reset fails or user locked out

**Scenario 2: OAuth Not Working**
- User: "Google sign-in worked before but now it doesn't"
- Expected response: Check pop-up blocker, enable third-party cookies, try incognito mode
- Escalate if: OAuth fails in incognito mode with all blockers disabled

**Scenario 3: Session Expires**
- User: "I log in but get logged out immediately"
- Expected response: Check cookie settings, clear cache, try different browser
- Escalate if: Happens in multiple browsers with cookies enabled

### Tools Access

**Required Access:**
- [ ] Zendesk/Support ticket system
- [ ] Entra ID admin portal (read-only for user lookup)
- [ ] Database (read-only for troubleshooting)
- [ ] Monitoring dashboard (Application Insights/Grafana)
- [ ] #migration-support Slack channel
- [ ] Status page admin panel

**Quick Links:**
- Entra ID Portal: https://portal.azure.com → Azure Active Directory
- App Insights: https://portal.azure.com → Application Insights → gymnastics-prod
- Status Page: https://status.gymnastics.example.com/admin
- Runbook: [link to PRODUCTION_MIGRATION_RUNBOOK.md]

---

## Common Issues & Solutions

### Issue 1: "I Can't Log In"

**Symptoms:**
- "Invalid email or password" error
- Login button does nothing
- Blank screen after login

**Troubleshooting Steps:**

**Step 1: Verify Email**
```
Support: "Can you confirm the email address you're using?"
User: "john.doe@example.com"
Support: [Check database]

Query:
SELECT "Email", "ProviderUserId", "TenantId"
FROM "UserProfiles"
WHERE "Email" ILIKE 'john.doe@example.com';

If found: Email exists, proceed to Step 2
If not found: Account doesn't exist, ask if they used different email to sign up
```

**Step 2: Password Reset**
```
Support: "Let's reset your password to make sure we have the right one."
User: "Okay"
Support:
1. Send them reset link: https://app.gymnastics.example.com/reset-password
2. Wait for confirmation email
3. Have them set new password
4. Try logging in again
```

**Step 3: Browser Check**
```
Support: "What browser are you using?"
User: "Chrome Version 130"
Support:
1. Ask them to clear cache and cookies
2. Try incognito mode
3. If incognito works: Cookie/cache issue
4. If incognito fails too: Escalate
```

**Step 4: OAuth Alternative**
```
Support: "Have you used Google Sign-In before?"
User: "Yes"
Support: "Let's try that instead. Click 'Sign in with Google' on the login page."

If Google Sign-In works: Their email/password might be incorrect
If Google Sign-In fails: OAuth issue (see Issue 3)
```

**When to Escalate:**
- Password reset email not received (check spam folder first)
- Reset link doesn't work
- Login fails in incognito mode after password reset
- User locked out (too many failed attempts)

**Escalation Template:**
```
Ticket: #12345
Issue: User cannot log in
User: john.doe@example.com
Tried: Password reset, cleared cache, incognito mode, Google OAuth
Result: All failed
Browser: Chrome 130 / Windows 11
Error: "Invalid credentials" (even after password reset)
Escalating to: Platform team
```

---

### Issue 2: "My Session Expires Immediately"

**Symptoms:**
- User logs in successfully
- Redirected to dashboard
- Immediately redirected back to login page
- Happens on every login attempt

**Root Cause:**
Browser not accepting/storing session cookies

**Troubleshooting Steps:**

**Step 1: Cookie Settings**
```
Support: "Let's check your browser's cookie settings."

Chrome:
1. Settings → Privacy and security → Cookies and other site data
2. Select "Allow all cookies" (or at least allow gymnastics.example.com)
3. Try logging in again

Firefox:
1. Settings → Privacy & Security → Cookies and Site Data
2. Select "Standard" mode
3. Try logging in again

Safari:
1. Preferences → Privacy
2. Uncheck "Block all cookies"
3. Try logging in again
```

**Step 2: Browser Extensions**
```
Support: "Do you have any privacy or ad-blocking extensions?"
User: "Yes, I have uBlock Origin and Privacy Badger"
Support: "Let's disable those temporarily:"

1. Click extension icon in browser toolbar
2. Disable extension
3. Refresh login page
4. Try logging in again

If it works: Extension was blocking cookies
Suggest: Whitelist gymnastics.example.com in extension settings
```

**Step 3: Third-Party Cookies**
```
Support: "We need to enable third-party cookies for OAuth to work."

Chrome:
1. Settings → Privacy → Cookies
2. Uncheck "Block third-party cookies"
3. Restart browser
4. Try again

Note: This is temporary - user can re-enable after login
```

**Step 4: Different Browser**
```
Support: "Let's try a different browser to isolate the issue."
User: "I'm using Firefox"
Support: "Can you try Chrome or Edge?"

If works in different browser: Original browser has persistent issue
If fails in all browsers: Server-side issue, escalate
```

**When to Escalate:**
- Session expires in ALL browsers (even with cookies enabled)
- Happens in incognito mode with no extensions
- Cookie is set but still redirects to login

**Escalation Template:**
```
Ticket: #12346
Issue: Session expires immediately after login
User: jane.smith@example.com
Tried: Enabled cookies, disabled extensions, tried Chrome/Firefox/Safari
Result: All failed
Browser: All browsers tested
Cookie Check: Session cookie IS being set (verified in DevTools)
Escalating to: Platform team
```

---

### Issue 3: "OAuth Login Doesn't Work"

**Symptoms:**
- Clicks "Sign in with Google/Microsoft"
- Nothing happens OR
- Pop-up opens then closes OR
- Error: "Redirect URI mismatch" OR
- Error: "Access denied"

**Troubleshooting Steps:**

**Step 1: Pop-Up Blocker**
```
Support: "Let's check if your browser is blocking the login pop-up."

1. Look for pop-up blocked icon in address bar (usually on the right)
2. Click it and select "Always allow pop-ups from app.gymnastics.example.com"
3. Refresh the page
4. Try OAuth login again

If pop-up now opens: Proceed to Step 2
If still nothing happens: Check browser extensions (Step 2)
```

**Step 2: Browser Extensions**
```
Support: "Privacy extensions can block OAuth. Let's disable them:"

1. Disable privacy/ad-blocking extensions (uBlock, Privacy Badger, etc.)
2. Refresh page
3. Try OAuth login again

If works: Extension was the issue
Suggest: Whitelist gymnastics.example.com in extension
```

**Step 3: Third-Party Cookies**
```
Support: "OAuth requires third-party cookies. Let's enable them:"

Chrome:
Settings → Privacy → Cookies → "Allow all cookies"

Firefox:
Settings → Privacy → Standard mode

Safari:
Preferences → Privacy → Uncheck "Prevent cross-site tracking"

Then try OAuth again.
```

**Step 4: OAuth Provider Check**
```
Support: "Which sign-in method are you using?"
User: "Google"
Support: "Are you signed into Google in your browser?"
User: "No"

Ask them to:
1. Sign into Google in another tab (gmail.com)
2. Return to login page
3. Try "Sign in with Google" again

For Microsoft:
1. Sign into Microsoft in another tab (outlook.com)
2. Return to login page
3. Try "Sign in with Microsoft" again
```

**Step 5: Alternative Login**
```
Support: "While we troubleshoot OAuth, you can use email/password login:"

1. Click "Sign in with Email" (or regular sign-in)
2. Enter email and password
3. This will get you in while we fix OAuth

If they don't remember password:
- Send password reset link
```

**When to Escalate:**
- "Redirect URI mismatch" error (configuration issue)
- OAuth works in staging but not production
- Happens in incognito mode with no extensions
- User's Google/Microsoft account is locked

**Escalation Template:**
```
Ticket: #12347
Issue: OAuth login fails
User: alice.johnson@example.com
Provider: Google / Microsoft
Tried: Disabled pop-up blocker, disabled extensions, enabled third-party cookies
Error: "Redirect URI mismatch" (or describe error)
Browser: Chrome 130 / macOS
Escalating to: Platform team
```

---

### Issue 4: "Where's the Microsoft Sign-In Button?"

**Symptoms:**
- User doesn't see "Sign in with Microsoft" button
- Only sees "Sign in with Google"

**Root Cause:**
- Frontend not updated yet (cached old version)
- User on old URL

**Troubleshooting Steps:**

**Step 1: Hard Refresh**
```
Support: "Let's refresh the page to get the latest version:"

Windows/Linux:
Ctrl + Shift + R (Chrome/Firefox)
Ctrl + F5 (most browsers)

Mac:
Cmd + Shift + R (Chrome/Firefox)
Cmd + Option + R (Safari)

Check if Microsoft button now appears.
```

**Step 2: Clear Cache**
```
Support: "Let's clear your browser cache:"

1. Press Ctrl+Shift+Delete (Windows) or Cmd+Shift+Delete (Mac)
2. Select:
   ☑ Cached images and files
   ☑ Cookies and other site data
3. Time range: "Last hour"
4. Click "Clear data"
5. Refresh login page

Microsoft button should now appear.
```

**Step 3: Check URL**
```
Support: "What URL are you on?"
User: "http://gymnastics.example.com"
Support: "Please use https://app.gymnastics.example.com instead."

Correct URLs:
✓ https://app.gymnastics.example.com
✗ http://gymnastics.example.com (old)
✗ http://app.gymnastics.example.com (not HTTPS)
```

**Step 4: Verify Frontend Version**
```
Support: "Can you open browser DevTools and check the console?"

1. Press F12 (Windows) or Cmd+Option+I (Mac)
2. Go to "Console" tab
3. Type: import.meta.env.VITE_AUTH_PROVIDER
4. Press Enter

Expected output: "entra"
If shows "keycloak": Frontend not updated, escalate
```

**When to Escalate:**
- Microsoft button missing after hard refresh and cache clear
- Frontend version check shows "keycloak" provider
- All users reporting missing button (deployment issue)

---

### Issue 5: "I Forgot My Password"

**Symptoms:**
- User doesn't remember password
- Never set password (only used OAuth before)

**Troubleshooting Steps:**

**Step 1: Password Reset**
```
Support: "No problem! Let's reset your password:"

1. Go to https://app.gymnastics.example.com/reset-password
2. Enter your email: [user's email]
3. Click "Send Reset Link"
4. Check your email (including spam folder)
5. Click the link in the email
6. Set a new password
7. Log in with new password

Password requirements:
- At least 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one number
- At least one special character (!@#$%^&*)
```

**Step 2: Email Not Received**
```
User: "I didn't get the email"
Support:

1. Check spam/junk folder
2. Wait 5 minutes (email delivery can be slow)
3. Try resending (visit reset page again)
4. Verify email address spelling
5. Check if email quota is full (unlikely but possible)

If still not received after 10 minutes: Escalate
```

**Step 3: OAuth Alternative**
```
Support: "Did you sign up using Google or Microsoft?"
User: "I used Google"
Support: "Great! You can skip the password and just use Google Sign-In:"

1. Go to https://app.gymnastics.example.com/sign-in
2. Click "Sign in with Google"
3. Select your Google account
4. You'll be logged in automatically

Note: You can set a password later in Profile Settings if you want.
```

**When to Escalate:**
- Password reset email not sent (check email service logs)
- Reset link doesn't work ("Invalid or expired link")
- User can't set new password (validation errors)

---

## Escalation Procedures

### When to Escalate

**Immediate Escalation (P1 - Critical):**
- Platform completely down
- Authentication broken for ALL users
- Database connectivity issues
- Security breach suspected

**Urgent Escalation (P2 - High):**
- Authentication broken for >10% of users
- Password reset not working
- OAuth broken for all users
- Session management broken

**Normal Escalation (P3 - Medium):**
- Individual user cannot log in (after troubleshooting)
- OAuth works for most users but not one specific user
- Unusual error messages

**Low Escalation (P4 - Low):**
- Feature questions
- Enhancement requests
- Non-blocking UI issues

### Escalation Path

**Level 1: Support Team**
- Handle common issues (login help, password reset, browser troubleshooting)
- Follow this guide
- Escalate after 30 minutes if unresolved

**Level 2: Platform Team**
- Slack: #migration-support
- Email: platform-team@gymnastics.example.com
- On-Call: [phone number]
- Handle technical issues, configuration problems, backend debugging

**Level 3: DevOps / Infrastructure**
- Slack: #devops-alerts
- Email: devops@gymnastics.example.com
- On-Call: [phone number]
- Handle server issues, deployment problems, database issues

**Level 4: CTO / Executive**
- For critical incidents >1 hour
- Security breaches
- Legal/compliance issues

### Escalation Template

```
Priority: [P1 / P2 / P3 / P4]
Ticket ID: #[number]
Reported By: [user email]
Issue: [one-line summary]

Description:
[Detailed description of issue]

Troubleshooting Steps Taken:
1. [Step 1]
2. [Step 2]
3. [Step 3]

Result:
[What happened]

Browser: [Chrome 130 / Firefox 125 / Safari 17]
OS: [Windows 11 / macOS 14 / Linux]
Error Messages:
[Exact error text or screenshot]

Impact:
- Number of users affected: [1 / few / many / all]
- Business impact: [Low / Medium / High / Critical]

Escalating To: [Platform Team / DevOps / CTO]
```

---

## Testing User Accounts

### Read-Only Database Access

**Connection String:**
```
# Available in 1Password vault: "Support DB Read-Only"
psql "postgresql://support_readonly:***@prod-db.postgres.azure.com:5432/gymnastics_prod?sslmode=require"
```

**Useful Queries:**

**Find user by email:**
```sql
SELECT
  "Id",
  "Email",
  "ProviderUserId",
  "TenantId",
  "CreatedAt",
  "LastModifiedAt"
FROM "UserProfiles"
WHERE "Email" ILIKE 'john.doe@example.com';
```

**Check user's roles:**
```sql
SELECT
  ur."Role",
  ur."AssignedAt",
  ur."AssignedBy"
FROM "UserRoles" ur
JOIN "UserProfiles" up ON ur."ProviderUserId" = up."ProviderUserId"
WHERE up."Email" ILIKE 'john.doe@example.com';
```

**Check recent logins:**
```sql
SELECT
  "PerformedAt",
  "Action",
  "IpAddress"
FROM "AuditLogs"
WHERE "PerformedByUserId" IN (
  SELECT "Id" FROM "UserProfiles" WHERE "Email" ILIKE 'john.doe@example.com'
)
AND "Action" = 'UserAuthenticated'
ORDER BY "PerformedAt" DESC
LIMIT 10;
```

**Count users by provider:**
```sql
SELECT
  CASE
    WHEN "ProviderUserId" LIKE '%-%-%-%-%' THEN 'EntraId'
    ELSE 'Keycloak'
  END AS provider,
  COUNT(*) AS user_count
FROM "UserProfiles"
GROUP BY provider;
```

### Entra ID Admin Portal (Read-Only)

**Access:** https://portal.azure.com → Azure Active Directory → Users

**Look Up User:**
1. Search by email in search bar
2. View user properties
3. Check "Authentication methods"
4. Check "Sign-in logs" (last 30 days)

**Check OAuth Federation:**
1. Azure Active Directory → Identity providers
2. Should see "Google" in list
3. Check "Status" is Enabled

**Check Extension Attribute:**
1. Find user
2. View "Extensions"
3. Look for `extension_{appid}_tenant_id`
4. Should match TenantId in database

---

## Support Scripts

### Script 1: Full Troubleshooting Checklist

```
User: "I can't log in"

Support: "I'm here to help! Let's troubleshoot step by step."

☐ Step 1: Verify email
"Can you confirm the email address you're trying to use?"
→ Check database if email exists

☐ Step 2: Try password reset
"Let's reset your password to make sure we have the right one."
→ Send reset link: https://app.gymnastics.example.com/reset-password

☐ Step 3: Check browser
"What browser are you using?"
→ Have them try incognito mode

☐ Step 4: Clear cache and cookies
"Let's clear your browser cache:"
→ Ctrl+Shift+Delete → Clear cached data

☐ Step 5: Try OAuth
"Have you used Google or Microsoft Sign-In before?"
→ Try OAuth as alternative

☐ Step 6: Check cookie settings
"Let's make sure your browser accepts cookies:"
→ Settings → Privacy → Allow cookies for gymnastics.example.com

☐ Step 7: Escalate if still failing
→ Escalate to platform team with details
```

### Script 2: Quick Win - OAuth Alternative

```
User: "I can't log in with my password"

Support: "No problem! Have you ever used Google Sign-In or Microsoft Sign-In on our platform?"

User: "Yes, I've used Google before"

Support: "Perfect! Let's use that instead:"

1. Go to https://app.gymnastics.example.com/sign-in
2. Click the "Sign in with Google" button
3. Select your Google account
4. You should be logged in!

Once you're in, you can reset your password from Profile Settings if you want to use email/password login in the future.

Did that work?
```

### Script 3: Session Expires Fix

```
User: "I log in but get kicked out immediately"

Support: "This usually means your browser isn't saving cookies. Let's fix that:"

Step 1: Enable cookies
[Browser-specific instructions]

Step 2: Clear existing cookies
Ctrl+Shift+Delete → Select "Cookies" → Clear

Step 3: Try logging in again

If it works: "Great! Your browser was blocking our session cookies. It should work fine now."

If it still fails: "Let's try a different browser to isolate the issue. Do you have Chrome/Firefox/Edge installed?"
```

---

## Monitoring During Migration

### Support Team Dashboard

**Metrics to Watch:**

1. **Ticket Volume** (Goal: < 100 on Day 1)
   - Real-time ticket count
   - Tickets by category
   - Average resolution time

2. **Authentication Success Rate** (Goal: > 95%)
   - Live from Application Insights
   - Updates every 5 minutes
   - Alert if drops below 90%

3. **Common Errors**
   - "Invalid credentials" count
   - "Redirect URI mismatch" count
   - "Session expired" count

4. **Response Times**
   - Average first response time
   - Average resolution time
   - Tickets exceeding SLA

### Daily Support Summary (Days 1-7)

**Send to #migration-support at end of each day:**

```
📊 Migration Support Summary - Day [N]

Tickets:
- Total: 45
- Resolved: 42
- Escalated: 3
- Open: 0

Top Issues:
1. Can't log in (40%)
2. Session expires (30%)
3. OAuth not working (20%)
4. Other (10%)

Escalations:
- Ticket #12345: OAuth redirect URI issue → Fixed by platform team
- Ticket #12346: User account locked → Unlocked in Entra
- Ticket #12347: Email reset not sent → Email service issue, resolved

Trends:
- Ticket volume decreasing (65 yesterday → 45 today)
- Average resolution time: 22 minutes (goal: <30 min)
- No critical incidents

Action Items:
- Update FAQ with top 3 issues
- Create macro for session expires issue

Team Feedback:
- [Any feedback from support agents]
```

---

## Post-Migration Support

### Week 1: Active Monitoring

**Daily Check-Ins:**
- 9 AM: Review overnight tickets
- 12 PM: Check authentication metrics
- 3 PM: Review escalations
- 5 PM: Daily summary to #migration-support

**Support Coverage:**
- Extended hours: 6 AM - 10 PM UTC (Days 1-3)
- Normal hours: 8 AM - 6 PM UTC (Days 4-7)

### Week 2: Stabilization

**Monitoring:**
- Daily summary (instead of multiple check-ins)
- Weekly review with platform team

**Ticket Volume Expectations:**
- Week 2: < 10 tickets/day
- Week 3: < 5 tickets/day
- Week 4+: Normal baseline

---

**Document Version:** 1.0
**Last Updated:** 2026-04-08
**Maintained By:** Support Team Lead
