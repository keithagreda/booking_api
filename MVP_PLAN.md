# MVP Plan: SportsHub Booking

## Problem & Solution
*Problem:* Sports hubs with multiple rooms (pickleball, billiards, darts, etc.) have no easy way to let players view availability and book slots online — leading to manual coordination and double-bookings.
*Solution:* A web app where players can browse available rooms by sport, pick a date and time slot, and confirm a booking — while admins manage rooms and reservations from a dashboard.

## Target Users
*Players* — casual and regular sports enthusiasts who want to reserve a room at the hub without calling or walking in. They need a fast, friction-free booking experience on any device.
*Admins* — hub staff who need to manage room listings, view upcoming bookings, and handle cancellations.

## Core Features (MoSCoW)

### Must Have
- Landing page with sports/room listings
- Date + time slot picker (per room)
- User registration and login (JWT)
- Guest booking prompt → redirects to sign-in/register before allowing booking
- Create and view bookings
- *Multi-room booking* — a single transaction can include multiple rooms/courts
- *Price snapshot* — booking records the room's price at time of booking, not current price
- GCash receipt upload (customer + admin)
- Admin approval/rejection of bookings
- Admin dashboard (view/cancel bookings, manage rooms)
- Room maintenance module (edit type, capacity, pricing, status)
- *Room Type maintenance* — admin can create, edit, and delete room types dynamically (fully flexible, not hardcoded)
- User ban (admin can ban accounts from booking)
- Room/court blocking (admin can temporarily block a room from being booked)

### Should Have
- Email confirmation on booking status change
- Booking cancellation by user
- Room availability status (real-time slot blocking)
- Multi-payment entry per transaction
- Audit logs (all user + admin actions recorded with timestamp, actor, entity, and change detail)
- SMTP settings page in admin (editable host, port, credentials + test email button)

### Could Have (Post-MVP)
- Custom calendar design/branding
- Walk-in / waitlist queue
- Recurring bookings
- SMS notifications
- Additional payment methods (cash, card, bank transfer)

### Won't Have (Out of Scope)
- Mobile app
- Multi-branch / multi-venue support
- Third-party calendar sync (Google Calendar, etc.)
- Automated payment verification (GCash API)
- Public API for partners

## Payment Flow
1. Customer completes booking → prompted to upload GCash receipt (image)
2. Admin reviews receipt image → approves or rejects the booking
3. Admin can also manually attach a receipt on the customer's behalf
4. A single booking/transaction can be split across *multiple payment entries* (e.g. partial GCash + cash top-up)
5. Booking status: Pending → Approved / Rejected

## Room Module
Rooms are managed by admin. Room types are *fully dynamic* — admins create and manage types (e.g. Pickleball Court, Billiard, Darts, Court, Room) via a dedicated maintenance page. No types are hardcoded. A room belongs to one type, but types can be freely added, renamed, or removed (soft delete to protect existing room references).

Each room has editable fields: name, type, capacity, price per hour, description, and active/inactive status. Rooms can be deactivated without deletion to preserve booking history. *Price is snapshotted at booking time* — changing a room's price later does not affect past transactions.

## Tech Stack
| Layer | Choice | Reason |
|-------|--------|--------|
| Frontend | Next.js (App Router) | SSR + routing out of the box |
| Backend | .NET Web API | Robust, typed, scalable REST API |
| Database | PostgreSQL | Relational, handles payments + booking relations cleanly |
| Auth | Custom JWT via .NET | Full control, no third-party dependency |
| File Storage | Local (MVP) → S3/Cloudinary later | Receipt image uploads |
| Email | SMTP / SendGrid | Booking status notifications |
| Hosting | Vercel (FE) + Railway or Render (BE) | Fast deploys, free tiers for MVP |

⚠️ **Assumptions:** GCash verification is manual (admin eyes receipt). Payment gateway integration (automated) is post-MVP. Admin users are seeded manually in the DB initially. Soft-deleted room types are hidden from new bookings but preserved on historical records.
