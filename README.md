# Palloncino Management System

Palloncino is a comprehensive management system designed for a party and balloon business. It streamlines operations by managing orders, inventory, job scheduling, and staff tasks across multiple branches.

## 🚀 Features

- **Order & Quotation Management:** Handle the entire customer lifecycle from initial quote to final order.
- **Inventory Control:** Real-time tracking of stock levels, movements, and item status.
- **Job & Task Scheduling:** Assign and monitor jobs for designers, drivers, and internal staff with dedicated role-based workflows.
- **Catalog Management:** Maintain a detailed catalog of products and services.
- **Multi-Branch Support:** Built to handle operations across various physical locations with branch-specific data isolation.
- **Automated Notifications:** Integration with Firebase for real-time push notifications to staff and customers.
- **Professional Reporting:** Generate high-quality PDF quotations and orders using QuestPDF.
- **Secure Authentication:** Robust JWT-based authentication with fine-grained Role-Based Access Control (RBAC).

## 🛠️ Tech Stack

- **Framework:** .NET 10 (ASP.NET Core Web API)
- **Database:** SQLite with Entity Framework Core
- **Security:** JWT Bearer Authentication, BCrypt password hashing
- **API Documentation:** [Scalar](https://scalar.com/) (Modern alternative to Swagger)
- **Mapping:** AutoMapper
- **Logging:** Serilog (Console & File)
- **Cloud Integration:** 
  - Azure Storage (Blobs, Tables, Queues)
  - Firebase Admin SDK (Notifications)
- **Reporting:** QuestPDF

## 🔌 API Endpoints

### 🔐 Authentication (`/api/Auth`)
- `POST /login` - User login & token generation (Public)
- `POST /register` - Customer self-registration (Public)
- `POST /refresh` - Refresh JWT access token
- `POST /logout` - Invalidate session
- `POST /change-password` - Update user password
- `POST /users/employee` - Create staff member (Admin)
- `POST /device-token` - Register Firebase device token for push notifications

### 📦 Catalog & Templates (`/api/catalog`, `/api/templates`)
- `GET /catalog` - List all catalog items (Public)
- `GET /catalog/{id}` - Get item details (Public)
- `POST /catalog` - Create catalog item (Admin)
- `PUT /catalog/{id}` - Update item (Admin)
- `GET /templates` - List party packages (Public)
- `GET /templates/{id}` - Package details & item breakdown (Public)
- `POST /templates` - Create new package (Admin)
- `POST /templates/{id}/duplicate` - Clone an existing template (Admin)

### 🛒 Orders (`/api/orders`)
- `POST /` - Place a regular order from catalog (Customer)
- `POST /custom` - Request a custom design order with attachments (Customer)
- `GET /my` - View current user's orders
- `GET /` - List all orders with filters (Admin/Employee)
- `PUT /{id}/approve` - Approve order & generate Job Order (Admin/Employee)
- `PUT /{id}/reject` - Reject order with reason (Admin/Employee)
- `PUT /{id}/status` - Manual status update (Admin/Employee)
- `GET /statistics` - Order volume and revenue analytics (Admin)

### 📄 Quotations (`/api/quotations`)
- `POST /` - Create a quote for a custom order (Admin/Employee)
- `GET /{id}` - View quotation details
- `GET /{id}/pdf` - Generate printable PDF quotation
- `PUT /{id}/approve` - Accept quotation (Customer/Admin)
- `PUT /{id}/reject` - Decline quotation (Customer/Admin)

### 🏗️ Job Orders (`/api/JobOrder`)
- `GET /` - List all active jobs sorted by due date (Admin/Employee)
- `GET /{id}/countdown` - Real-time countdown to delivery deadline
- `PUT /{id}/status` - Update overall job status (Admin/Employee)

### 📋 Tasks & Execution (`/api/tasks`)
- `GET /tasks` - List assigned tasks for current user
- `PUT /tasks/{id}/start` - Mark task as in-progress
- `PUT /tasks/{id}/complete` - Mark task as finished
- `PUT /tasks/{id}/skip` - Skip a task with justification (Admin/Employee)
- `POST /tasks/{id}/subtasks` - Add granular sub-tasks (Admin/Employee)
- `PUT /tasks/{id}/checklist` - Toggle checklist items (Loading, Preparation, Delivery)
- `POST /tasks/{id}/inventory` - Link inventory items used during execution
- `GET /tasks/dashboard` - Operational overview of all tasks (Admin/Employee)

### 🎨 Design Tasks (`/api/tasks/.../design`)
- `GET /tasks/{id}/design` - Design brief and reference images
- `POST /tasks/{id}/design/uploads` - Upload design proposals/previews (Designer)
- `PUT /tasks/{id}/design/status` - Move to `pending_review` or `completed`
- `POST /tasks/{id}/design/feedback` - Add comments on designs (Customer/Admin)

### 🏭 Inventory (`/api/Inventory`)
- `GET /items` - List inventory with stock levels (Admin/Employee)
- `POST /item` - Add new inventory item (Admin)
- `POST /item/{id}/stock/add` - Increase stock (Admin/Employee)
- `POST /item/{id}/stock/remove` - Decrease stock/Waste logging (Admin/Employee)
- `POST /transfer` - Move stock between branches (Admin)
- `GET /low-stock` - Alert list for reordering (Admin/Employee)
- `GET /report` - Full inventory valuation report (Admin)

## 📁 Project Structure

```text
Palloncino/
├── Controllers/         # API Endpoints (Auth, Orders, Inventory, etc.)
├── Services/            # Business Logic Layer (Interfaces & Implementations)
├── Models/              # Data Models
│   ├── Entities/        # Database Entities
│   ├── DTOs/            # Data Transfer Objects
│   └── Enums/           # System Constants/Enums
├── Data/                # DbContext and Migrations
├── Mappers/             # AutoMapper Profiles
├── Logs/                # Application Logs
└── Properties/          # Launch Settings
```

## ⚙️ Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [EF Core Tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) (`dotnet tool install --global dotnet-ef`)

### Installation & Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/your-repo/Palloncino.git
   cd Palloncino
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Configure the application:**
   Update `appsettings.json` with your local settings (JWT keys, Firebase credentials, etc.).

4. **Initialize the database:**
   The application uses SQLite. Run the following to apply migrations:
   ```bash
   dotnet ef database update
   ```

5. **Run the application:**
   ```bash
   dotnet run
   ```

## 📖 API Documentation

Once the application is running, you can access the interactive API documentation (Scalar) at:
`http://localhost:<port>/scalar/v1`

## 🔧 Configuration

Key configuration sections in `appsettings.json`:

- `ConnectionStrings`: SQLite database location.
- `Jwt`: Security settings for token generation and validation.
- `Firebase`: Credentials for push notifications.
- `FileStorage`: Local path and size limits for attachments.
- `BusinessRules`: Tunable parameters for system behavior (e.g., default due hours).

## 🛡️ Role-Based Access

The system supports the following roles:
- `Admin`: Full system access.
- `Employee`: General staff operations.
- `Designer`: Specialized job views for design tasks.
- `Driver`: Delivery-focused access.
- `Customer`: Limited access for order tracking.

## 📄 License

This project is licensed under the [MIT License](LICENSE).
