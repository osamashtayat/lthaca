# Users

`User` accounts (Identity, in Infrastructure, read here as `UserReadModel`) and their profile (api-contract.md §14):
`ProfileService` (display name, password change, account deletion), its requests, `ProfileDto` and `ProfileErrors`.
Sessions, sign-in and guests are in `../Auth/`. Copy the shape of `../Films/` (the reference slice); see
`docs/backend-conventions.md`.
