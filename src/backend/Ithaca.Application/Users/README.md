# Users

`User` accounts (Identity, in Infrastructure, read here as `UserReadModel`) and their profile (api-contract.md §14):
`ProfileService` (display name, password change, account deletion), its requests, `ProfileDto` and `ProfileErrors`;
`PasswordResetService` (forgot / reset password by email, api-contract.md §4) with its requests and errors; the email goes through
`../Email/` (`IEmailSender`, `EmailOptions`, templates).
Sessions, sign-in and guests are in `../Auth/`. Copy the shape of `../Films/` (the reference slice); see
`docs/backend-conventions.md`.
