# Requirements

## Assignment (source)

Customer portal:
- [ ] Place a maintenance order
- [ ] View order details/status by reference, email, or phone number
- [ ] Add or update notes before the repair has started
- [ ] Confirmation after submitting the order

Admin page:
- [ ] View, search, and filter orders
- [ ] Edit or delete an order
- [ ] Change status (New, In progress, Waiting for customer, Completed, Cancelled)
- [ ] Basic capacity / due-date information

Order data: customer name, phone, email, bike brand, service type (Wheel adjustment, Chain replacement, Brake maintenance), expected due date, notes (optional).

Technical:
- [ ] Persisted in a database
- [ ] Input validation and meaningful error handling
- [ ] Basic automated tests
- [ ] API documentation or architectural overview
- [ ] Separation between customer and admin functionality

Business logic (at least one):
- [ ] Service pricing with total cost and discount codes
- [ ] Due-date validation: max five orders per day, suggest next available date
- [ ] Order status lifecycle with valid/invalid transitions

Optional change request: estimated price shown before submit; admin can override the final price.

## Interpretation and assumptions

### Customers and accounts

- **Customers don't need an account.** The spec has customers find orders "using a simple reference, email, or phone number" and lists contact details as order data, so guest ordering is the default flow.
- **Customer accounts are optional.** A logged-in customer gets prefilled contact details and a list of their orders. The order still stores its own contact details either way.
- **Staff must log in.** Admin and Mechanic are roles on a real user account (ASP.NET Core Identity). This is how customer and admin functionality are separated.

### Order lookup

- **Lookup requires the order reference plus the email or phone on the order**, sent in the `Order-Contact` request header (`GET /api/orders/{reference}`). Read literally, "reference, email, or phone" would let anyone who knows a phone number see that person's orders and edit their notes. Requiring two factors works like an airline booking lookup (booking reference + last name).
- **References are short but not guessable:** 8 characters from an alphabet without look-alike characters (no `0/O`, `1/I/L`), e.g. `BB-7K3QX9MD`. They are never sequential, since they act as half of the lookup credential.
- The lookup endpoint is rate-limited to slow down guessing.

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

- **Capacity is 5 orders per due date** (a configuration value). Per-date exceptions such as holidays are not a requirement and are left out.
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
