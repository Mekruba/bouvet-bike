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

## Interpretation

<!-- TODO -->

## Priorities

<!-- TODO -->

## Out of scope

Per the assignment: production auth, payments, email/SMS, pixel-perfect design, cloud deployment.
