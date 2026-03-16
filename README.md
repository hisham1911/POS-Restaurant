<div align="center">

# 🏪 TajerPro

### Modern Point of Sale System | نظام نقاط البيع الحديث

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18-61DAFB?style=flat-square&logo=react)](https://reactjs.org/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.7-3178C6?style=flat-square&logo=typescript)](https://www.typescriptlang.org/)
[![TailwindCSS](https://img.shields.io/badge/Tailwind-3.4-06B6D4?style=flat-square&logo=tailwindcss)](https://tailwindcss.com/)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)

A full-featured, production-ready Point of Sale system built with Clean Architecture principles.

[Features](#-features) • [Quick Start](#-quick-start) • [Deployment](#-deployment) • [Documentation](#-documentation)

</div>

---

## 🚀 Quick Start للمطورين

### بناء جميع الإصدارات:

```powershell
cd Deployment\Scripts
.\BUILD_ALL.ps1
```

**النتيجة:** 4 ملفات تثبيت جاهزة في `Deployment\Installers\`

### للتطوير المحلي:

```bash
# Backend
cd backend/KasserPro.API
dotnet run

# Frontend
cd frontend
npm install
npm run dev
```

---

## 📦 Deployment

### الإصدارات المتاحة:

| الملف                       | نظام التشغيل | المعمارية | الحجم   |
| --------------------------- | ------------ | --------- | ------- |
| TajerPro-Setup.exe          | Win10/11     | x64       | ~124 MB |
| TajerPro-Setup-x86.exe      | Win10/11     | x86       | ~74 MB  |
| TajerPro-Setup-Win7-x64.exe | Win7 SP1+    | x64       | ~79 MB  |
| TajerPro-Setup-Win7-x86.exe | Win7 SP1+    | x86       | ~71 MB  |

### 📁 Deployment Structure:

```
Deployment/
├── Installers/       → ملفات التثبيت النهائية
├── ISS/              → Inno Setup configurations
├── Scripts/          → BUILD_ALL.ps1
└── Docs/             → التوثيق الكامل
```

**📚 للتفاصيل الكاملة:** [Deployment/Docs/BUILD_GUIDE.md](Deployment/Docs/BUILD_GUIDE.md)

---

## ✨ Features

| Feature                   | Description                                         |
| ------------------------- | --------------------------------------------------- |
| 🛒 **POS Interface**      | Fast, intuitive sales interface with real-time cart |
| 📦 **Product Management** | Full CRUD for products and categories               |
| 📋 **Order Management**   | Track and manage all orders with status workflow    |
| ⏰ **Shift Management**   | Open/close shifts with cash tracking                |
| 📊 **Daily Reports**      | Sales reports with payment breakdown                |
| 💰 **Tax Management**     | Configurable tax rates (Tax Exclusive model)        |
| 🌐 **RTL Support**        | Full Arabic language support                        |
| 📱 **Responsive**         | Works on desktop, tablet, and mobile                |
| 🔐 **Authentication**     | JWT-based auth with role management (Admin/Cashier) |
| 🏢 **Multi-Tenant**       | Built-in multi-tenancy support                      |

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Frontend                              │
│              React 18 + TypeScript + Vite                    │
│              Redux Toolkit + RTK Query                       │
└─────────────────────────┬───────────────────────────────────┘
                          │ REST API (JWT Auth)
┌─────────────────────────▼───────────────────────────────────┐
│                      API Layer                               │
│                   ASP.NET Core 9                             │
├─────────────────────────────────────────────────────────────┤
│                  Application Layer                           │
│              Services, DTOs, Validators                      │
├─────────────────────────────────────────────────────────────┤
│                    Domain Layer                              │
│              Entities, Enums, Interfaces                     │
├─────────────────────────────────────────────────────────────┤
│                Infrastructure Layer                          │
│           EF Core + SQLite + Audit Interceptors              │
└─────────────────────────────────────────────────────────────┘
```

## 📁 Project Structure

```
KasserPro/
├── 📦 backend/                      # Backend Source Code
│   ├── KasserPro.API/              # REST API & Controllers
│   ├── KasserPro.Application/      # Business Logic & DTOs
│   ├── KasserPro.Domain/           # Entities & Interfaces
│   ├── KasserPro.Infrastructure/   # Data Access & Services
│   ├── KasserPro.BridgeApp/        # Desktop Bridge (WPF)
│   └── KasserPro.Tests/            # Unit & Integration Tests
│
├── 🌐 frontend/                     # Frontend Source Code
│   ├── src/
│   │   ├── api/                    # RTK Query APIs
│   │   ├── components/             # Reusable Components
│   │   ├── hooks/                  # Custom React Hooks
│   │   ├── pages/                  # Page Components
│   │   ├── store/                  # Redux Store & Slices
│   │   ├── types/                  # TypeScript Definitions
│   │   └── utils/                  # Helper Functions
│   └── e2e/                        # Playwright E2E Tests
│
├── 📚 project-resources/            # Documentation & Tools
│   ├── docs/                       # All Documentation
│   │   ├── deployment/             # Deployment Guides
│   │   ├── features/               # Feature Documentation
│   │   ├── fixes/                  # Bug Fix Reports
│   │   ├── guides/                 # User Guides (AR/EN)
│   │   ├── reports/                # Technical Reports
│   │   └── archive/                # Archived Documents
│   ├── scripts/                    # Automation Scripts
│   │   ├── deployment/             # Build & Deploy
│   │   ├── database/               # SQL Scripts
│   │   ├── testing/                # Test Scripts
│   │   └── maintenance/            # Maintenance
│   ├── tools/                      # Additional Tools
│   │   ├── migration-helpers/      # Migration Utilities
│   │   └── KasserPro.Installer/    # Installer Project
│   └── output/                     # Build Outputs (gitignored)
│       ├── packages/               # Deployment Packages
│       └── installers/             # Installer Files
│
├── 🏗️ .github/                     # GitHub Workflows
├── 🔧 .kiro/                       # Kiro Specs
├── 🎯 .vscode/                     # VS Code Settings
│
├── 📄 README.md                    # Main Documentation
├── 📄 LICENSE                      # MIT License
├── 📄 KasserPro.sln                # Visual Studio Solution
├── 📄 .gitignore                   # Git Ignore Rules
└── 📄 .editorconfig                # Editor Configuration
```

## 🚀 Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/)
- [Git](https://git-scm.com/)

### Installation

```bash
# Clone the repository
git clone https://github.com/yourusername/KasserPro.git
cd KasserPro

# Start Backend
cd backend/KasserPro.API
dotnet restore
dotnet run

# Start Frontend (new terminal)
cd frontend
npm install
npm run dev
```

### Access

| Service      | URL                           |
| ------------ | ----------------------------- |
| Frontend     | http://localhost:3000         |
| Backend API  | http://localhost:5243/api     |
| Swagger Docs | http://localhost:5243/swagger |

### Demo Credentials

| Role    | Email               | Password  |
| ------- | ------------------- | --------- |
| Admin   | admin@kasserpro.com | Admin@123 |
| Cashier | ahmed@kasserpro.com | 123456    |

## 🧪 Testing

### E2E Tests (Playwright)

```bash
cd client

# Run all E2E tests
npm run test:e2e

# Run with browser visible
npm run test:e2e:headed

# Run with Playwright UI
npm run test:e2e:ui
```

### Integration Tests (.NET)

```bash
cd src/KasserPro.Tests
dotnet test
```

### Test Scenarios

| Scene   | Description                       |
| ------- | --------------------------------- |
| Scene 1 | Admin Setup - Tax configuration   |
| Scene 2 | Cashier Workday - Full order flow |
| Scene 3 | Security Guard - Negative testing |
| Scene 4 | Report Verification               |

## 📖 Documentation

### 📚 Main Documentation Folders

| Folder                                                                       | Description                                        |
| ---------------------------------------------------------------------------- | -------------------------------------------------- |
| [**project-resources/docs/**](project-resources/docs/)                       | Complete documentation hub                         |
| [**project-resources/docs/deployment/**](project-resources/docs/deployment/) | Deployment guides & production readiness           |
| [**project-resources/docs/features/**](project-resources/docs/features/)     | Feature documentation & user guides                |
| [**project-resources/docs/guides/**](project-resources/docs/guides/)         | Quick start & how-to guides (AR/EN)                |
| [**project-resources/docs/reports/**](project-resources/docs/reports/)       | Technical reports & audits                         |
| [**project-resources/scripts/**](project-resources/scripts/)                 | Automation scripts (database, testing, deployment) |

### 📄 Key Documents

| Document                                                                                             | Description                            |
| ---------------------------------------------------------------------------------------------------- | -------------------------------------- |
| [Architecture Manifest](project-resources/docs/KASSERPRO_ARCHITECTURE_MANIFEST.md)                   | **المرجع الأساسي** - القواعد والمعايير |
| [Deployment Guide](project-resources/docs/deployment/DEPLOYMENT_GUIDE_COMPLETE.md)                   | Complete production deployment guide   |
| [Production Readiness Audit](project-resources/docs/deployment/PRODUCTION_READINESS_AUDIT_REPORT.md) | Production readiness assessment        |
| [API Reference](project-resources/docs/api/API_DOCUMENTATION.md)                                     | Complete API documentation             |
| [System Health Report](project-resources/docs/SYSTEM_HEALTH_REPORT.md)                               | Audit findings and fixes               |
| [Design System](project-resources/docs/design/DESIGN_SYSTEM.md)                                      | UI/UX design guidelines                |

### 🚀 Quick Access

**Want to deploy?** → [project-resources/docs/deployment/DEPLOYMENT_GUIDE_COMPLETE.md](project-resources/docs/deployment/DEPLOYMENT_GUIDE_COMPLETE.md)  
**Need help?** → [project-resources/docs/guides/](project-resources/docs/guides/)  
**Found a bug?** → [project-resources/docs/fixes/](project-resources/docs/fixes/)  
**Technical details?** → [project-resources/docs/reports/](project-resources/docs/reports/)

## 🛠️ Tech Stack

### Backend

- **.NET 9** - Web API Framework
- **Entity Framework Core 9** - ORM
- **SQLite** - Database
- **JWT** - Authentication
- **Clean Architecture** - Design Pattern

### Frontend

- **React 18** - UI Library
- **TypeScript 5.7** - Type Safety
- **Redux Toolkit** - State Management
- **RTK Query** - Data Fetching
- **TailwindCSS** - Styling
- **Vite 6** - Build Tool
- **Playwright** - E2E Testing

## 💰 Financial Logic

KasserPro uses **Tax Exclusive (Additive)** model:

```
Net Total = Unit Price × Quantity
Tax Amount = Net Total × (Tax Rate / 100)
Total = Net Total + Tax Amount
```

- Default Tax Rate: 14% (Egypt VAT)
- Configurable per tenant via Admin settings
- All prices stored as NET (excluding tax)

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guide](CONTRIBUTING.md) for details.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

<div align="center">

**Built with ❤️ for the Egyptian Market**

</div>
