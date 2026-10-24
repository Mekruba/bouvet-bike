# Requirements

## Assignment (source)

Customer portal:
- [x] Place a maintenance order
- [x] View order details/status by reference, email, or phone number
- [x] Add or update notes before the repair has started
- [x] Confirmation after submitting the order

Admin page:
- [x] View, search, and filter orders
- [x] Edit or delete an order
- [x] Change status (New, In progress, Waiting for customer, Completed, Cancelled)
- [x] Basic capacity / due-date information

Order data: customer name, phone, email, bike brand, service type (Wheel adjustment, Chain replacement, Brake maintenance), expected due date, notes (optional).

Technical:
- [x] Persisted in a database
- [x] Input validation and meaningful error handling
- [x] Basic automated tests
- [x] API documentation or architectural overview
- [x] Separation between customer and admin functionality

Business logic (at least one):
- [x] Service pricing with total cost and discount codes
- [x] Due-date validation: max five orders per day, suggest next available date
- [x] Order status lifecycle with valid/invalid transitions

- [x] Optional change request: estimated price shown before submit; admin can override the final price. **Implemented:** the booking page shows a live estimate (`GET /api/price-quotes`, which checks a discount code without using it up), and admins set a final price with a reason the customer sees (`PUT /api/admin/orders/{id}/price-override`). The calculated price is kept alongside the override.

## Interpretation and assumptions

### Customers and accounts

- **Customers don't need an account.** The spec has customers find orders "using a simple reference, email, or phone number" and lists contact details as order data, so guest ordering is the default flow.
- **Customer accounts are optional.** A logged-in customer gets prefilled contact details and a list of their orders. The order still stores its own contact details either way.
- **Customers confirm their email before they can log in.** Registering creates the account and a one-time confirmation link (Identity's email confirmation token). The password is typed twice on the registration page; that check is in the UI only, since the API doesn't need it.
- **There is no email delivery** (out of scope), so the API returns the link that would have been emailed (`emailConfirmationLink`) and the frontend opens it directly: the confirm page the customer would reach from their inbox. With real email the field goes away, and receiving the link is what proves the address is theirs.
- **Confirming the email links your guest orders.** Orders booked as a guest with the same email (ignoring case) are linked to the account and show up under "My orders". Orders already on another account stay there, and phone numbers are not matched. Only a confirmed email claims orders, which with real email delivery would stop anyone claiming someone else's bookings. In this simulation the link is in the response, so that protection only exists once real email is in place.
- Staff accounts are created by an admin (and the first admin from configuration), so their email counts as confirmed.

- **Staff must log in.** Admin and Mechanic are roles on a real user account (ASP.NET Core Identity). This is how customer and admin functionality are separated.

### Order lookup

- **A guest finds orders with *either* the reference *or* the email or phone number**, as the assignment says ("a simple reference, email, or phone number"):
  - **The reference alone opens the order** (`GET /api/orders/{reference}`) and lets the customer change notes. It works like a private link: it's random, so knowing it means you were given it.
  - **Email or phone alone lists every order with that contact**, any status, newest first (`GET /api/orders` with the `Order-Contact` header, so personal data stays out of URLs). Each result links to its order.
- **Trade-off, accepted on purpose:** anyone who knows someone's email or phone number can see that person's bookings and notes. An earlier version required reference *and* contact (like an airline booking lookup). It was changed to match the assignment's wording and keep lookup simple for customers. In a real product this would be closed with a one-time link or code sent to the email or phone, which is out of scope here (no email/SMS integration).
- **References are short but not guessable:** 8 characters from an alphabet without look-alike characters (no `0/O`, `1/I/L`), e.g. `BB-7K3QX9MD`. They are never sequential, since the reference alone gives access.
- **Lookups are rate-limited per client** (30 a minute by default, `RateLimiting:OrderLookups:PermitsPerMinute`): over the limit gives 429 with `Retry-After`. This slows down guessing references and harvesting orders by contact.

### Bikes

- **Bike brand is free text** (with suggestions in the UI), plus an optional model. Bikes are not stored as separate records; a registered customer's order list is their service history.

### Contact details

- **Contact details (name, email, phone) are stored on the order as a snapshot,** even for logged-in customers. The order records who to contact *for this repair*. Changing your account profile does not rewrite old orders.
- **Phone numbers are stored in international format** (`+4791234567`). Numbers without a country code are assumed to be Norwegian and must have 8 digits, so `912 34 567` and `+47 912 34 567` match on lookup.
- Emails are compared case-insensitively (stored in lower case).

### Notes

- **"Before the repair has started" means status `New`.** Once an order moves to `In progress` or later, customers can no longer change their notes.
- An order has one **customer notes** field. Customers (guest or logged in) can add, change or clear it while the order is `New`.
- Staff have a separate **internal notes** field that customers never see, editable at any status.

### Services and pricing

- **One service per order,** as in the spec. A customer with two jobs places two orders.
- Service types are a catalog (seeded with Wheel adjustment, Chain replacement, Brake maintenance) with a base price and an estimated duration, so prices and services can change without a deploy. Service type names are unique (case-insensitive). Service types are never deleted, since orders reference them; they are deactivated, which hides them from customers.
- **The service name and price are copied onto the order when it is placed.** Later catalog changes do not alter existing orders.
- Discount codes are percentage or fixed amount, with an optional validity period and usage limit. The code's terms are stored on the order, so the discount follows the price if staff change the service.
- One discount code per order. A fixed discount never makes the total negative.
- **Admins can override the final price,** with a required reason. The override, who made it and why are stored alongside the calculated price, never replacing it.

### Capacity and due dates

- **Each weekday has a default capacity** that admins set in the staff area (e.g. Mon–Fri 5, Sat 3, Sun 0 = closed). It starts at the configured `Capacity:MaxOrdersPerDay` (5) for every day.
- **A single date can deviate** from its weekday default: extra capacity, less, or 0 to close it (holidays, short-staffed days), with an optional reason. Staff see it against the default, e.g. "6/5". Past dates can't be changed.
- Lowering a date below what's already booked keeps the existing orders; the date simply shows as full.
- Only orders that are not `Cancelled` count toward capacity.
- When a requested date is full, the API rejects it and suggests the next available date.
- The due date cannot be in the past; today is allowed. Staff can still edit an order whose due date has passed without changing the date.

### Status lifecycle

- **Every status change is recorded** (from, to, who, when, optional reason), so the lifecycle has a history and not just a current value.
- Valid transitions are enforced by the order itself; an invalid transition is a business rule violation (400).

## Priorities

<!-- TODO -->

## Out of scope

Per the assignment: payments, email/SMS, pixel-perfect design, cloud deployment.

Production-grade auth is not expected. ASP.NET Core Identity is still used, since real roles are the cleanest way to separate staff from customers and leave room to grow. Email confirmation, password reset by email, 2FA and external login providers are left out.
