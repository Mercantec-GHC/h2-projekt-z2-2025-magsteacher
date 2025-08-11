## H2-2025

```mermaid
erDiagram
  Users {
    Id text PK
    Email text 
    Username text 
    HashedPassword text 
    Salt text 
    LastLogin timestamp with time zone 
    PasswordBackdoor text 
    CreatedAt timestamp with time zone 
    UpdatedAt timestamp with time zone 
  }
```
