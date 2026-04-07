# Migration User Communications

This document contains all user-facing communication templates for the Entra ID migration. Customize with your brand voice and specific dates before sending.

---

## T-7 Days: Initial Announcement

**Subject:** Scheduled Maintenance - Saturday, [Date] at 6:00 AM UTC

**From:** Platform Team <noreply@gymnastics.example.com>

**To:** All Users

**Content:**

```
Hi [FirstName],

We're writing to let you know about scheduled maintenance on Saturday, [Date] at 6:00 AM UTC (convert to your timezone).

What's happening:
We're upgrading our authentication system to Microsoft Entra ID for improved security and reliability.

What you need to know:
• Platform will be unavailable for approximately 2 hours
• You'll be logged out automatically
• Your email and password will continue to work
• New option: Sign in with Microsoft (in addition to Google)
• No data will be lost

When:
Start: Saturday, [Date] at 6:00 AM UTC
End: Saturday, [Date] at 8:00 AM UTC (estimated)

What you need to do:
1. Save any work before 6:00 AM UTC on Saturday
2. Log out before maintenance begins
3. After maintenance, log back in with your existing credentials

We've chosen this low-traffic time to minimize disruption. If you have questions, reply to this email or contact support@gymnastics.example.com.

Thank you for your patience!

The Gymnastics Platform Team

---

Your timezone:
[Link to timezone converter]
```

**Delivery:**
- Email to all active users (last login within 30 days)
- In-app notification banner (7 days before)
- Post to community forum / social media

---

## T-3 Days: Reminder

**Subject:** Reminder: Maintenance in 3 Days - Saturday, [Date]

**From:** Platform Team <noreply@gymnastics.example.com>

**To:** All Users

**Content:**

```
Hi [FirstName],

This is a reminder about our scheduled maintenance in 3 days.

When:
Saturday, [Date] at 6:00 AM UTC
Duration: Approximately 2 hours

What to expect:
✓ Platform unavailable during maintenance
✓ You'll be logged out automatically
✓ Your existing login credentials will work
✓ New "Sign in with Microsoft" option available
✓ Improved security and faster logins

Mark your calendar:
[Add to Google Calendar] [Add to Outlook]

Questions? Contact support@gymnastics.example.com

Thank you!
The Gymnastics Platform Team
```

**Delivery:**
- Email to all users
- In-app notification banner (persist until maintenance)
- Push notification (if mobile app exists)

---

## T-1 Day: Final Reminder

**Subject:** Tomorrow: Maintenance at 6:00 AM UTC

**From:** Platform Team <noreply@gymnastics.example.com>

**To:** All Users

**Content:**

```
Hi [FirstName],

Final reminder: Our maintenance window starts tomorrow.

⏰ Saturday, [Date] at 6:00 AM UTC (that's [LocalTime] your time)

⏱️ Expected duration: 2 hours

Before maintenance:
[ ] Save all your work
[ ] Log out of the platform
[ ] Expect to be unable to access the platform for ~2 hours

After maintenance:
[ ] Log back in with your existing email and password
[ ] Try the new "Sign in with Microsoft" option
[ ] Contact support if you have any issues

We'll post updates on our status page: https://status.gymnastics.example.com

See you on the other side!
The Gymnastics Platform Team
```

**Delivery:**
- Email to all users
- In-app notification (red banner)
- Status page update

---

## T-0: Maintenance Starting

**Subject:** Maintenance In Progress

**From:** Platform Team <noreply@gymnastics.example.com>

**To:** All Users (Optional - some orgs skip this)

**Content:**

```
Hi [FirstName],

Our scheduled maintenance is now in progress.

Status: 🔴 Maintenance Mode
Expected completion: 8:00 AM UTC (~2 hours)

You cannot access the platform right now. We'll send another email when maintenance is complete.

Track progress: https://status.gymnastics.example.com

Thank you for your patience!
The Gymnastics Platform Team
```

**Delivery:**
- Email (optional - can be noisy)
- Status page update (required)
- Social media post

---

## T+0: Maintenance Complete (Success)

**Subject:** ✅ Maintenance Complete - Platform Available

**From:** Platform Team <noreply@gymnastics.example.com>

**To:** All Users

**Content:**

```
Hi [FirstName],

Great news! Our maintenance is complete and the platform is back online.

Status: ✅ Operational

What's new:
✓ Upgraded authentication (Microsoft Entra ID)
✓ Sign in with Microsoft now available
✓ Improved security and reliability
✓ Faster login times

What you need to do:
1. Visit https://app.gymnastics.example.com
2. Log in with your existing email and password
   (or use "Sign in with Google/Microsoft")
3. Continue using the platform as normal

Having trouble logging in?
• Make sure you're using the correct email and password
• Clear your browser cache and cookies
• Try "Sign in with Google" if you previously used it
• Contact support@gymnastics.example.com if issues persist

Thank you for your patience during this upgrade!

The Gymnastics Platform Team
```

**Delivery:**
- Email to all users
- In-app notification (green banner - dismiss after 24 hours)
- Status page update
- Social media post

---

## T+0: Maintenance Complete (With Issues)

**Use this version if migration succeeded but some users are experiencing issues**

**Subject:** ⚠️ Maintenance Complete - Platform Available (Some Users May Experience Issues)

**From:** Platform Team <noreply@gymnastics.example.com>

**To:** All Users

**Content:**

```
Hi [FirstName],

Our maintenance is complete and the platform is back online. However, some users are experiencing login issues. We're actively working on this.

Status: 🟡 Partially Operational

What's working:
✓ Platform accessible
✓ Most users can log in successfully
✓ All data preserved

What's not working for some users:
⚠️ OAuth login (Google/Microsoft) for ~5% of users
⚠️ Password reset emails delayed

What you can do:
1. Try logging in as normal
2. If login fails, wait 15 minutes and try again
3. Clear browser cache and cookies
4. Contact support@gymnastics.example.com if issues persist >30 minutes

We're monitoring the situation closely and will update you within 1 hour.

Status page: https://status.gymnastics.example.com

We apologize for the inconvenience.
The Gymnastics Platform Team
```

**Delivery:**
- Email to all users
- In-app notification (yellow banner)
- Status page update
- Social media post with link to status page

**Follow-up:** Send resolution email once issues are fixed

---

## T+0: Rollback (Maintenance Postponed)

**Use this if rollback was executed**

**Subject:** ⚠️ Maintenance Postponed Due to Technical Issues

**From:** Platform Team <noreply@gymnastics.example.com>

**To:** All Users

**Content:**

```
Hi [FirstName],

We encountered technical issues during today's maintenance and have postponed the upgrade.

Status: ✅ Operational (on previous system)

What happened:
We attempted to upgrade our authentication system but discovered issues that required us to postpone the migration. We've restored the platform to its previous state.

What this means for you:
✓ Platform fully operational
✓ Continue using your existing login (no changes)
✓ All data preserved
✓ No action required from you

New maintenance date:
We'll analyze what went wrong and schedule a new maintenance date. We'll notify you at least 7 days in advance.

Why we prioritize stability:
We'd rather delay an upgrade than risk platform stability or data loss. Your trust is our top priority.

Questions? Contact support@gymnastics.example.com

We apologize for the inconvenience and thank you for your understanding.

The Gymnastics Platform Team
```

**Delivery:**
- Email to all users (high priority)
- In-app notification (blue banner)
- Status page update
- Social media post
- Community forum post with FAQ

---

## T+24 Hours: Check-In Email

**Subject:** How's Your Experience After the Upgrade?

**From:** Platform Team <noreply@gymnastics.example.com>

**To:** All Users

**Content:**

```
Hi [FirstName],

It's been 24 hours since our authentication upgrade. We wanted to check in.

Quick survey (30 seconds):
[Have you logged in since the upgrade?]
[ ] Yes, no issues
[ ] Yes, but had minor issues
[ ] Yes, but had major issues
[ ] No, not yet
[ ] No, unable to log in

[How would you rate the new login experience?]
⭐⭐⭐⭐⭐

[Any comments?]
[Text box]

[Submit]

Issues?
If you're still experiencing problems, contact support@gymnastics.example.com with details.

What's new:
• Sign in with Microsoft now available
• Faster login times (30% improvement)
• Enhanced security
• Better mobile experience

Thank you for helping us improve!

The Gymnastics Platform Team
```

**Delivery:**
- Email to users who logged in within 7 days before migration
- Optional - can be skipped if no issues reported

---

## T+7 Days: Migration Success Summary

**Subject:** Upgrade Complete - Thank You!

**From:** Platform Team <noreply@gymnastics.example.com>

**To:** All Users

**Content:**

```
Hi [FirstName],

One week ago, we upgraded our authentication system. Here's how it went:

By the numbers:
✓ 500+ users migrated successfully
✓ 97.8% login success rate
✓ 30% faster login times
✓ 0 critical incidents
✓ 98% positive user feedback

What you told us you love:
💬 "Login is so much faster now!"
💬 "Microsoft Sign-In is super convenient"
💬 "No issues at all, seamless transition"

Thank you for your patience during the upgrade. Your feedback helped us ensure a smooth migration.

New features coming soon:
🚀 Multi-factor authentication (optional)
🚀 Biometric login (mobile app)
🚀 Enhanced session management

Stay tuned!

The Gymnastics Platform Team

P.S. We're always improving. Got suggestions? Reply to this email!
```

**Delivery:**
- Email to all users
- Blog post (if applicable)
- Social media post celebrating success

---

## Support Response Templates

### Issue: User Can't Log In

**Response:**

```
Hi [FirstName],

Thanks for reaching out. Let's get you logged in.

Please try these steps:
1. Go to https://app.gymnastics.example.com/sign-in
2. Enter your email address exactly as it appears in this email: [Email]
3. Enter your password (case-sensitive)
4. Click "Sign In"

If that doesn't work:
• Try "Sign in with Google" or "Sign in with Microsoft"
• Reset your password: https://app.gymnastics.example.com/reset-password
• Clear browser cache and cookies, then try again

Still having trouble? Reply to this email with:
- What browser are you using? (Chrome, Firefox, Safari, etc.)
- What error message do you see?
- Screenshot (if possible)

We're here to help!

Support Team
support@gymnastics.example.com
```

### Issue: Session Expires Immediately

**Response:**

```
Hi [FirstName],

It sounds like your browser isn't accepting cookies. Let's fix that.

Steps:
1. Check your browser's cookie settings:
   • Chrome: Settings → Privacy → Cookies (allow all or allow gymnastics.example.com)
   • Firefox: Settings → Privacy → Cookies (allow all or allow gymnastics.example.com)
   • Safari: Preferences → Privacy → Cookies (uncheck "Block all cookies")

2. Clear existing cookies:
   • Press Ctrl+Shift+Delete (Windows) or Cmd+Shift+Delete (Mac)
   • Select "Cookies" and "Cached data"
   • Click "Clear data"

3. Try logging in again

Still not working? Try:
• Disable browser extensions (especially privacy/ad blockers)
• Try incognito/private mode
• Try a different browser

Let me know if this helps!

Support Team
support@gymnastics.example.com
```

### Issue: OAuth Login Not Working

**Response:**

```
Hi [FirstName],

Thanks for reporting this. Let's troubleshoot your [Google/Microsoft] Sign-In issue.

Common causes:
✓ Pop-up blocker enabled
✓ Third-party cookies disabled
✓ Browser extension blocking OAuth

Steps to fix:
1. Allow pop-ups for app.gymnastics.example.com:
   • Look for a pop-up blocked icon in your browser's address bar
   • Click it and select "Always allow pop-ups from this site"

2. Enable third-party cookies (temporarily):
   • Chrome: Settings → Privacy → Cookies → "Allow all cookies"
   • Firefox: Settings → Privacy → Standard mode
   • Safari: Preferences → Privacy → Uncheck "Prevent cross-site tracking"

3. Try OAuth login again

Alternative:
If OAuth still doesn't work, you can:
• Use email/password login instead
• Reset your password if you forgot it: [Reset link]

We're here if you need more help!

Support Team
support@gymnastics.example.com
```

---

## Status Page Messages

### Pre-Migration (T-7 to T-0)

```
Status: 🟢 Operational

Upcoming Maintenance:
Saturday, [Date] at 6:00 AM UTC - 8:00 AM UTC

Reason: Authentication system upgrade (Microsoft Entra ID)
Impact: Platform unavailable for ~2 hours
Action Required: None (log back in after maintenance)

More info: [Link to blog post]
```

### During Migration (T-0)

```
Status: 🔴 Maintenance

Scheduled Maintenance In Progress

Start: 6:00 AM UTC
Expected End: 8:00 AM UTC
Current Time: [Live clock]

Upgrading authentication system. Platform will be back online shortly.

Last Update: [Timestamp]
Next Update: In 30 minutes
```

### Post-Migration (Success)

```
Status: 🟢 Operational

All Systems Operational

Maintenance completed at [Time].
Authentication upgraded to Microsoft Entra ID.

New features:
• Sign in with Microsoft
• Improved security
• Faster login times

Having issues? Contact support@gymnastics.example.com
```

### Post-Migration (Issues)

```
Status: 🟡 Degraded Performance

Some users experiencing login issues

Issue: OAuth login fails for ~5% of users
Workaround: Use email/password login
ETA: 1 hour

We're actively investigating. Updates every 15 minutes.

Last Update: [Timestamp]
```

### Post-Rollback

```
Status: 🟢 Operational

Migration Postponed

Maintenance attempted but postponed due to technical issues.
Platform restored to previous state. No data lost.

New migration date: TBA (will announce 7 days in advance)

All systems operating normally.
```

---

## FAQ Document (Post-Migration)

**Post this to your knowledge base / help center**

### Frequently Asked Questions - Authentication Upgrade

**Q: Why was I logged out?**
A: We upgraded our authentication system. All users were logged out as part of the migration. Simply log back in with your existing credentials.

**Q: Do I need to create a new account?**
A: No! Your existing account still works. Use the same email and password you've always used.

**Q: What's Microsoft Entra ID?**
A: It's Microsoft's enterprise authentication service (formerly Azure Active Directory). It provides better security, reliability, and new features like "Sign in with Microsoft."

**Q: Can I still use Google Sign-In?**
A: Yes! Google Sign-In still works exactly as before.

**Q: What's new with this upgrade?**
A: You can now use "Sign in with Microsoft" in addition to email/password and Google Sign-In. The system is also faster and more secure.

**Q: I forgot my password. How do I reset it?**
A: Click "Forgot Password" on the sign-in page, enter your email, and follow the instructions in the reset email.

**Q: I'm getting an "Invalid redirect URI" error. What do I do?**
A: Clear your browser cache and cookies, then try again. If that doesn't work, contact support@gymnastics.example.com.

**Q: My session expires immediately after logging in. Why?**
A: Your browser might be blocking cookies. Check your browser's privacy settings and allow cookies for gymnastics.example.com.

**Q: Can I use my Microsoft work account to sign in?**
A: Yes! "Sign in with Microsoft" supports both personal Microsoft accounts and work/school accounts.

**Q: Is my data safe?**
A: Absolutely. No data was lost or modified during the migration. We only upgraded the authentication system.

**Q: I'm still having issues. Who do I contact?**
A: Email support@gymnastics.example.com with details about your issue. Include what browser you're using and any error messages you see.

---

**Document Version:** 1.0
**Last Updated:** 2026-04-08
**Maintained By:** Platform Team
