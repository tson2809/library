# Hệ Thống Quản Lý Thư Viện (Library Management System)

## 1. Tổng Quan Dự Án
Dự án **Library** là một hệ thống quản lý thư viện hiện đại được xây dựng để đáp ứng các nhu cầu nghiệp vụ toàn diện cho các thư viện quy mô vừa và lớn. Hệ thống cung cấp khả năng quản lý chi tiết từ thông tin đầu sách, bản sao vật lý, quy trình mượn/trả, đến nghiệp vụ thu phí phạt và quản lý thông báo nội bộ.

Được phát triển trên nền tảng **ASP.NET Core / .NET 8**, hệ thống tách biệt ứng dụng hiển thị `Frontend` và backend `Backend`. Backend hiện có RESTful Web API chuẩn PRN232 dùng JWT Bearer Authentication, Swagger/OpenAPI và EF Core SQL Server.

## 2. Cấu Trúc Dự Án

- **Frontend (ASP.NET Core MVC)**
  - Ứng dụng server-rendered MVC cho nhân viên/quản lý.
  - Duy trì session đăng nhập, lưu JWT server-side và gọi REST API qua các typed REST client theo domain.
  - Có `Controllers`, `Models/ViewModels`, `Views`, `Services`, `wwwroot`.

- **Backend (ASP.NET Core Web API)**
  - RESTful API theo resource: `/api/auth`, `/api/books`, `/api/members`, `/api/loans`, `/api/users`, `/api/reports`, v.v.
  - JWT Bearer auth, role policies `manager` / `staff`, Swagger UI.

- **Database (`library.sql`)**
  - SQL Server schema + seed data cho demo/local.
  - Script có tính destructive vì drop/create database. Xem `docs/database.md` trước khi chạy lại.

- **Tests**
  - `tests/Server.Tests`
  - `tests/Client_web.Tests`

- **Local tooling**
  - Chạy Server và Client_web bằng Visual Studio hoặc `dotnet run`.
  - `.github/workflows/dotnet.yml` build/test CI nếu repository bật GitHub Actions.

## 3. Toàn Bộ Chức Năng

### 3.1. Xác thực và đăng nhập
- Đăng nhập bằng tài khoản nội bộ thư viện.
- Đăng nhập bằng Google Identity Services: Client_web nhận Google ID token, Server xác thực token rồi cấp JWT nội bộ cho tài khoản đã được cấp quyền trong hệ thống.
- JWT access token được phát hành bởi Server và lưu trong session phía Client_web.
- Kiểm tra trạng thái tài khoản còn hoạt động trước khi cho phép truy cập.
- Mật khẩu tài khoản nội bộ được băm bằng PBKDF2-SHA256 có salt; tài khoản cũ dùng SHA256 sẽ tự nâng cấp hash sau lần đăng nhập hợp lệ đầu tiên.
- Đăng xuất và xóa phiên làm việc.

### 3.2. Hồ sơ cá nhân người dùng
- Xem/cập nhật hồ sơ, số điện thoại, ảnh đại diện.
- Đổi mật khẩu có xác nhận mật khẩu mới.

### 3.3. Quản lý nhân sự
- Quản lý tài khoản nhân sự, tạo/sửa/khóa/mở tài khoản, đặt lại mật khẩu.
- Lọc, tìm kiếm, phân trang danh sách nhân sự.

### 3.4. Quản lý danh mục đầu sách
- Xem danh sách/chi tiết đầu sách.
- Tìm kiếm, lọc, tạo/cập nhật/ngừng khai thác đầu sách.
- Quản lý tác giả, nhà xuất bản, thể loại, ảnh bìa và số lượng bản sao.
- Tra cứu ISBN qua Google Books/OpenLibrary.

### 3.5. Quản lý thể loại
- Xem/tạo/tìm kiếm/lọc/phân trang thể loại.

### 3.6. Quản lý bản sao vật lý
- Quản lý barcode, trạng thái bản sao, tình trạng vật lý, vị trí lưu trữ.

### 3.7. Quản lý thành viên
- Xem/tạo/cập nhật thành viên, trạng thái hoạt động, công nợ và trạng thái mượn.

### 3.8. Quản lý mượn/trả/gia hạn
- Tạo phiếu mượn theo thành viên và barcode.
- Kiểm tra giới hạn mượn, công nợ, quá hạn, trạng thái bản sao.
- Trả sách theo barcode, tính phạt tự động, gia hạn phiếu mượn.

### 3.9. Quản lý đặt trước
- Tạo/xem yêu cầu đặt trước, giữ bản sao theo hàng chờ khi sách được trả.

### 3.10. Thu phạt và báo cáo
- Thu tiền phạt, lịch sử thanh toán, dashboard, báo cáo top sách/thành viên, quá hạn, doanh thu, xuất Excel.

### 3.11. Thông báo và nhật ký
- Tạo/gửi/xem thông báo nội bộ.
- Audit/system logs cho các thao tác quan trọng.

## 4. REST API và Swagger

Server chạy mặc định tại:

- `http://localhost:5099`

Swagger UI:

- `http://localhost:5099/swagger`

Ví dụ endpoint REST chính:

```http
POST /api/auth/login
GET  /api/books
GET  /api/books/{bookId}
POST /api/books
PUT  /api/books/{bookId}
POST /api/books/{bookId}/deactivate
GET  /api/members
POST /api/members
GET  /api/loans
POST /api/loans
POST /api/loans/return-by-barcode
GET  /api/reports/overdue-loans
```

Header cần có cho `/api/*`:

```http
X-Api-Key: <internal-api-key>
Authorization: Bearer <jwt-token>   # với endpoint protected
```

`X-Api-Key` là credential nội bộ giữa Client_web và Server. JWT là user identity/authorization mechanism.

## 5. OData endpoint

Project có thêm endpoint OData read-only cho danh sách sách để demo chương OData trong PRN232:

```http
GET /odata/books
GET /odata/books?$top=10
GET /odata/books?$filter=contains(tolower(Title),'java')
GET /odata/books?$orderby=BorrowCount desc&$select=BookId,Title,BorrowCount
GET /odata/$metadata
```

Header cần có cho `/odata/*`:

```http
X-Api-Key: <internal-api-key>
```

Endpoint này chỉ expose DTO an toàn `BookODataDto`, không expose entity `User` hoặc trường nhạy cảm.

## 6. Cấu hình môi trường

Không lưu secret production trong source code. Cấu hình qua biến môi trường hoặc secret store khi chạy ứng dụng.

### Server

- `ConnectStrings__DBConnection` hoặc `ConnectionStrings__DBConnection`: connection string SQL Server.
- `Api__Key`: API key nội bộ để Client_web gọi Server.
- `Http__Url`: URL lắng nghe của Server, mặc định `http://localhost:5099`.
- `Jwt__Issuer`: issuer JWT, ví dụ `LibraryServer`.
- `Jwt__Audience`: audience JWT, ví dụ `LibraryClient`.
- `Jwt__SigningKey`: khóa ký JWT, tối thiểu 32 bytes.
- `Jwt__AccessTokenMinutes`: thời lượng access token, mặc định 480 phút.
- `IsbnLookup__GoogleBooksApiKey`: Google Books API key để tra cứu ISBN ổn định hơn và giảm lỗi rate limit `429 Too Many Requests`.
- `IsbnLookup__IsbnDbApiKey`: ISBNdb API key tùy chọn, nếu muốn dùng ISBNdb làm nguồn tra cứu đầu tiên.
- `Sql__ConnectTimeoutSeconds`, `Sql__CommandTimeoutSeconds`, `Sql__MaxPoolSize`, `Sql__RetryCount`, `Sql__RetryDelaySeconds`, `Sql__ConnectRetryCount`, `Sql__ConnectRetryIntervalSeconds`: cấu hình SQL tùy chọn.

Ví dụ local:

```bash
export ConnectStrings__DBConnection='Data Source=localhost;Initial Catalog=library;User ID=sa;Password=<password>;TrustServerCertificate=True'
export Api__Key='<internal-api-key>'
export Jwt__Issuer='LibraryServer'
export Jwt__Audience='LibraryClient'
export Jwt__SigningKey='<at-least-32-byte-signing-key>'
export IsbnLookup__GoogleBooksApiKey='<google-books-api-key>'
dotnet run --project Backend/Backend/Backend.csproj
```

### Client_web

- `ServerApi__BaseUrl`: URL Server, ví dụ `http://localhost:5099`.
- `ServerApi__ApiKey`: cùng giá trị với `Api__Key` của Server.
- `GoogleAuth__ClientId` hoặc `Authentication__Google__ClientId`: Google OAuth web client ID, chỉ cần khi bật Google Identity Services login. Không cần client secret vì Client_web dùng ID token flow.
- Trong Google Cloud Console, thêm `http://localhost:7000` vào **Authorized JavaScript origins** cho local; flow này không dùng redirect URI `/signin-google`.

Ví dụ local:

```bash
export ServerApi__BaseUrl='http://localhost:5099'
export ServerApi__ApiKey='<internal-api-key>'
dotnet run --project Frontend/Frontend/Frontend.csproj
```

## 7. Database workflow

Xem chi tiết tại `docs/database.md`.

Tóm tắt:

- `library.sql` là source of truth schema + seed cho local/demo.
- Script sẽ drop/create database nên chỉ dùng khi muốn reset DB.
- EF migrations chưa dùng trong scope hiện tại để tránh schema drift.

## 8. Chạy local

Chạy Server và Client_web trực tiếp bằng Visual Studio hoặc .NET CLI:

```bash
dotnet run --project Backend/Backend/Backend.csproj
dotnet run --project Frontend/Frontend/Frontend.csproj
```

Ứng dụng:

- Server: `http://localhost:5099`
- Swagger: `http://localhost:5099/swagger`
- Client_web: `http://localhost:7000`

## 9. Build và test

```bash
dotnet restore Backend/Backend.sln
dotnet restore Frontend/Frontend.sln
dotnet build Backend/Backend.sln --configuration Release --no-restore
dotnet build Frontend/Frontend.sln --configuration Release --no-restore
dotnet test tests/Server.Tests/Server.Tests.csproj
dotnet test tests/Client_web.Tests/Client_web.Tests.csproj
```

CI workflow nằm tại `.github/workflows/dotnet.yml`.

## 10. Ghi chú REST API

Server chỉ expose các REST endpoint theo resource như `/api/auth`, `/api/books`, `/api/members`, `/api/loans`, `/api/reports`, v.v. Client_web gọi các endpoint này qua typed REST client theo domain (`IAuthApiClient`, `IBooksApiClient`, `ILoansApiClient`, v.v.), không còn adapter action-string/gateway legacy.
