# RequestSpark

[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License](https://img.shields.io/github/license/MakeBoldSolutions/RequestSpark)](https://github.com/MakeBoldSolutions/RequestSpark/blob/main/LICENSE)
[![GitHub Issues](https://img.shields.io/github/issues/MakeBoldSolutions/RequestSpark)](https://github.com/MakeBoldSolutions/RequestSpark/issues)
[![GitHub Stars](https://img.shields.io/github/stars/MakeBoldSolutions/RequestSpark)](https://github.com/MakeBoldSolutions/RequestSpark/stargazers)
[![Release](https://img.shields.io/github/v/release/MakeBoldSolutions/RequestSpark)](https://github.com/MakeBoldSolutions/RequestSpark/releases/latest)

**Live Site**: [https://request.makeboldspark.com](https://request.makeboldspark.com)

A comprehensive .NET 10 solution for running REST API tests, performance benchmarking, and regression testing using Postman collections.

## About

RequestSpark is a .NET 10 web application and console tool for running REST API tests, performance benchmarking, and regression testing against Postman collections. It provides a browser-based dashboard for managing API configurations, executing test runs, and reviewing results — with built-in support for load testing, OpenAPI specs, and CSV exports.

The live instance is hosted at [https://request.makeboldspark.com](https://request.makeboldspark.com).

> Built by [Mark Hazleton](https://markhazleton.com) — Mark Hazleton, Solutions Architect
> RequestSpark is part of the [Make Bold Spark](https://makeboldspark.com) portfolio of technical demonstrations.

> **Latest Update** (v10.0.0): Upgraded to .NET 10.0 (LTS) with 19% faster builds, 25% faster tests, and comprehensive package optimization!

## Features

- **Postman Collection Integration**: Import and execute existing Postman collections
- **Automated Regression Testing**: Run comprehensive API test suites automatically
- **Performance Analysis**: Compare response times across multiple API instances
- **Load Testing**: Stress test your REST APIs with configurable parameters
- **Detailed Reporting**: Export results to CSV with comprehensive statistics
- **Web Interface**: Razor Pages web application for interactive testing
- **Sample CRUD API**: Built-in sample API for testing and demonstration
- **Comprehensive Statistics**: Response time percentiles, success rates, and performance metrics
- **High Performance**: 19% faster builds, 25% faster test execution
- **Secure**: 93% packages at latest versions, zero vulnerabilities

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Performance](#performance)
- [Changelog](#changelog)
- [Migration Guide](#migration-guide)
- [Contributing](#contributing)
- [Documentation](#documentation)

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later (LTS)
- Windows, macOS, or Linux
- Visual Studio 2022 17.12+ or VS Code (recommended)

### Why .NET 10?

RequestSpark uses **.NET 10.0 (LTS)** for:

- **Long-term support**: 3 years of support (until November 2028)
- **Performance**: Significant improvements in build and test performance
- **Security**: Latest security patches and updates
- **Compatibility**: Modern API features and optimizations

## Installation

### Clone the Repository

```bash
git clone https://github.com/MakeBoldSolutions/RequestSpark.git
cd RequestSpark
```

### Build the Solution

```bash
dotnet build
# Expected output: Project RequestSpark.sln has been successfully built.
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test RequestSpark.Domain.Tests

# Expected output: 21/21 tests passing (100%)
# Test execution time: ~0.6s (25% faster than .NET 9)
```

## Quick Start

### Console Application

1. **Prepare your Postman collection**: Place your `collection.json` file in the `RequestSpark` project directory
2. **Run the console application**:

   ```bash
   cd RequestSpark
   dotnet run
   ```

3. **View results**: Check the generated CSV file at `c:\test\RequestSpark.csv`

### Web Application

1. **Start the web server**:

   ```bash
   cd RequestSpark.Web
   dotnet run
   ```

## Performance

RequestSpark v10.0.0 delivers significant performance improvements:

### Build Performance

- **Build Time**: 19% faster (5.1s -> 4.1s)
- **Test Execution**: 25% faster (0.8s -> 0.6s)
- **Package Optimization**: 11.8% fewer dependencies

### Package Health

- **Latest Versions**: 93% packages at latest stable versions
- **Security**: Zero vulnerabilities
- **Framework-Included**: Optimized package references for .NET 10

### Quality Metrics

- **Test Pass Rate**: 100% (21/21 tests)
- **Build Errors**: 0
- **Code Quality**: MSTest v4 analyzers enabled for continuous improvement

For detailed upgrade information, see [Upgrade Documentation](.github/upgrades/upgrade-complete-summary.md).

## Changelog

### v10.0.0 (2025-12-23) - .NET 10 LTS Upgrade

**Major Changes:**

- Upgraded all projects to .NET 10.0 (LTS)
- Build performance improved by 19% (5.1s -> 4.1s)
- Test performance improved by 25% (0.8s -> 0.6s)
- Package optimization: 17 -> 15 packages (93% at latest)
- Removed framework-included packages (System.Text.Json, System.Security.Cryptography.Xml)
- Updated WebSpark packages to latest (Bootswatch 1.34.0, HttpClientUtility 2.1.2)
- MSTest v4 with code quality analyzers
- Comprehensive upgrade documentation

**Quality Improvements:**

- All 21 tests passing (100%)
- Zero build errors
- NU1510 warnings eliminated
- MSTest v4 code quality analyzers enabled

**Documentation:**

- Added comprehensive upgrade documentation in `.github/upgrades/`
- Created GitHub issues for future improvements (#2, #3)

See [CHANGELOG.md](https://github.com/MakeBoldSolutions/RequestSpark/blob/main/CHANGELOG.md) for complete version history.

## Migration Guide

The product identity is now fully standardized as RequestSpark across active source, runtime surfaces, and current documentation.

| Surface            | Current Identifier            |
| ------------------ | ----------------------------- |
| Solution | `RequestSpark.sln` |
| Console project | `RequestSpark` |
| Domain project | `RequestSpark.Domain` |
| Import project | `RequestSpark.PostmanImport` |
| Web project | `RequestSpark.Web` |
| Domain test project | `RequestSpark.Domain.Tests` |
| Web test project | `RequestSpark.Web.Tests` |
| Namespace root | `RequestSpark.*` |
| Exception family | `RequestSparkException*` |
| Console CSV output | `RequestSpark.csv` |

### Intentional Historical References

The lingering-brand audit is clean for active runtime code and project metadata. Remaining former-name references are intentional historical records:

- `.github/upgrades/assessment.md` (generated anchor IDs and graph labels preserved for report integrity)
- `.github/upgrades/assessment.csv` and `.github/upgrades/assessment.json` (source analyzer output snapshots)
- `.github/security/DEPENDABOT-ANALYSIS.md` and `.github/security/VULNERABILITIES-RESOLVED.md` (historical security incident documentation)
- `.documentation/**` historical/specification/audit artifacts (including quickfix and planning records retained for traceability)

### Post-Merge Repository Rename Checklist

1. Ensure the GitHub repository name is `RequestSpark`.
2. Confirm badges and links resolve under the new repository path.
3. Update any remaining GitHub URLs in active `.github/` docs to the renamed repository.
4. Re-run the lingering-brand audit and keep only explicitly historical exceptions.

## Contributing

Repository engineering rules are defined in `.documentation/memory/constitution.md`.
Until CI is added, contributors are expected to run `dotnet build` and `dotnet test`
locally before merging changes. Run `npm run build` when editing
`RequestSpark.Web` asset-pipeline inputs.
Executable code paths must not introduce hardcoded secrets or deployable
credential defaults; use configuration-backed values or clearly non-secret
placeholders instead.

### Reporting Issues

When reporting issues, please include:

- .NET 10 SDK version (`dotnet --version`)
- Operating system
- Steps to reproduce
- Expected vs actual behavior
- Sample Postman collection (if applicable)

## Documentation

- **Engineering Constitution**: See [.documentation/memory/constitution.md](.documentation/memory/constitution.md)
- **Upgrade Guide**: See [.github/upgrades/upgrade-complete-summary.md](.github/upgrades/upgrade-complete-summary.md)
- **Assessment Report**: [.github/upgrades/assessment.md](.github/upgrades/assessment.md)
- **Migration Plan**: [.github/upgrades/plan.md](.github/upgrades/plan.md)
- **Package Optimization**: [.github/upgrades/package-review-report.md](.github/upgrades/package-review-report.md)
