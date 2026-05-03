# Library Management System (Lumina)

ASP.NET Core 8 MVC library suite with catalogue, circulation (checkouts, returns, overdue), reservations, fines, and a responsive **Lumina** dashboard UI. Uses **Entity Framework Core** with **MySQL**.

## Repository layout

| Path | Description |
|------|----------------|
| `Library Management System.sln` | Visual Studio solution |
| `Library Management System/` | Web application project (`Controllers`, `Views`, `Models`, `Data`, `wwwroot`) |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [MySQL Server](https://dev.mysql.com/downloads/mysql/) (local or remote)

## Configuration

1. Copy connection settings into `Library Management System/appsettings.json` (or use **User Secrets** / environment variables).
2. Replace the placeholder password with your MySQL `root` (or app user) password:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Port=3306;Database=LibraryDB;User=root;Password=YOUR_PASSWORD;"
}
```

Do **not** commit real passwords to GitHub.

## Run locally

```powershell
cd "Library Management System"
dotnet restore
dotnet ef database update --project "Library Management System.csproj"
dotnet run --project "Library Management System.csproj"
```

Then open the URL shown in the terminal (typically `http://localhost:5xxx`).

If `dotnet ef` is unavailable:

```powershell
dotnet tool install --global dotnet-ef
```

## Tech stack

- ASP.NET Core 8 MVC, Razor views, Bootstrap 5
- EF Core + Pomelo MySQL provider
- Barcoded copies (`BookItem`), branches, publishers, categories, reservations, fines

## License

Use and modify as needed for your coursework or project.
