-- Hệ thống quản lý thư viện
USE master;
GO
DROP DATABASE IF EXISTS library;
GO
CREATE DATABASE library;
GO
USE library;
GO

-- 1) Bảng vai trò
CREATE TABLE dbo.Roles (
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    RoleName VARCHAR(30) NOT NULL UNIQUE,
    Description NVARCHAR(200) NULL
);

-- 2) Bảng người dùng nội bộ (staff/manager)
CREATE TABLE dbo.Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username VARCHAR(50) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    FullName NVARCHAR(120) NOT NULL,
    Email VARCHAR(120) NULL UNIQUE,
    Phone VARCHAR(20) NULL,
    AvatarUrl VARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    RoleId INT NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
);

-- 3) Bảng tác giả
CREATE TABLE dbo.Authors (
    AuthorId INT IDENTITY(1,1) PRIMARY KEY,
    AuthorName NVARCHAR(150) NOT NULL,
    DateOfBirth DATE NULL,
    Nationality NVARCHAR(80) NULL
);

-- Bảng thông báo nội bộ
CREATE TABLE dbo.Notifications (
    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(250) NOT NULL,
    Content NVARCHAR(4000) NOT NULL,
    CreatedByUserId INT NOT NULL,
    SendToAll BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId)
);

CREATE TABLE dbo.NotificationRecipients (
    NotificationRecipientId INT IDENTITY(1,1) PRIMARY KEY,
    NotificationId INT NOT NULL,
    RecipientUserId INT NOT NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    ReadAt DATETIME2 NULL,
    CONSTRAINT FK_NotificationRecipients_Notifications FOREIGN KEY (NotificationId) REFERENCES dbo.Notifications(NotificationId) ON DELETE CASCADE,
    CONSTRAINT FK_NotificationRecipients_Users FOREIGN KEY (RecipientUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT UQ_NotificationRecipients_Notification_User UNIQUE (NotificationId, RecipientUserId)
);

-- 4) Bảng thể loại
CREATE TABLE dbo.Categories (
    CategoryId INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(250) NULL
);

-- 5) Bảng nhà xuất bản
CREATE TABLE dbo.Publishers (
    PublisherId INT IDENTITY(1,1) PRIMARY KEY,
    PublisherName NVARCHAR(150) NOT NULL UNIQUE,
    AddressLine NVARCHAR(250) NULL,
    Phone VARCHAR(20) NULL,
    Email VARCHAR(120) NULL
);

-- 6) Bảng sách
CREATE TABLE dbo.Books (
    BookId INT IDENTITY(1,1) PRIMARY KEY,
    ISBN VARCHAR(20) NOT NULL UNIQUE,
    Title NVARCHAR(250) NOT NULL,
    ImageUrl VARCHAR(500) NULL,
    PublisherId INT NULL,
    PublishedYear INT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Books_Publishers FOREIGN KEY (PublisherId) REFERENCES dbo.Publishers(PublisherId),
    CONSTRAINT CHK_Books_PublishedYear CHECK (PublishedYear IS NULL OR (PublishedYear BETWEEN 1000 AND YEAR(GETDATE()) + 1))
);

-- 7) Quan hệ nhiều-nhiều: Sách <-> Thể loại
CREATE TABLE dbo.BookCategories (
    BookId INT NOT NULL,
    CategoryId INT NOT NULL,
    PRIMARY KEY (BookId, CategoryId),
    CONSTRAINT FK_BookCategories_Books FOREIGN KEY (BookId) REFERENCES dbo.Books(BookId) ON DELETE CASCADE,
    CONSTRAINT FK_BookCategories_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId)
);

-- 8) Quan hệ nhiều-nhiều: Sách <-> Tác giả
CREATE TABLE dbo.BookAuthors (
    BookId INT NOT NULL,
    AuthorId INT NOT NULL,
    PRIMARY KEY (BookId, AuthorId),
    CONSTRAINT FK_BookAuthors_Books FOREIGN KEY (BookId) REFERENCES dbo.Books(BookId) ON DELETE CASCADE,
    CONSTRAINT FK_BookAuthors_Authors FOREIGN KEY (AuthorId) REFERENCES dbo.Authors(AuthorId)
);

-- 9) Bảng bản sao vật lý của sách
CREATE TABLE dbo.BookCopies (
    BookCopyId INT IDENTITY(1,1) PRIMARY KEY,
    BookId INT NOT NULL,
    Barcode VARCHAR(5) NOT NULL UNIQUE,
    AcquiredDate DATE NULL,
    CopyStatus VARCHAR(20) NOT NULL DEFAULT 'Available',
    PhysicalCondition NVARCHAR(30) NOT NULL DEFAULT N'New',
    LocationCode VARCHAR(30) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_BookCopies_Books FOREIGN KEY (BookId) REFERENCES dbo.Books(BookId) ON DELETE CASCADE,
    CONSTRAINT CHK_BookCopies_Barcode CHECK (Barcode LIKE '[0-9][0-9][0-9][0-9][0-9]'),
    CONSTRAINT CHK_BookCopies_CopyStatus CHECK (CopyStatus IN ('Available', 'Borrowed', 'Reserved', 'Lost', 'Damaged', 'Maintenance', 'Disposed')),
    CONSTRAINT CHK_BookCopies_PhysicalCondition CHECK (PhysicalCondition IN (N'New', N'Good', N'Worn', N'Damaged', N'Lost'))
);

-- 10) Bảng thành viên thư viện
CREATE TABLE dbo.Members (
    MemberId INT IDENTITY(1,1) PRIMARY KEY,
    MemberCode VARCHAR(20) NOT NULL UNIQUE,
    FullName NVARCHAR(120) NOT NULL,
    DateOfBirth DATE NULL,
    Gender VARCHAR(10) NULL,
    Email VARCHAR(120) NULL UNIQUE,
    Phone VARCHAR(20) NULL,
    AddressLine NVARCHAR(250) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CHK_Members_Gender CHECK (Gender IS NULL OR Gender IN ('Male', 'Female', 'Other'))
);

-- 11) Bảng phiếu mượn
CREATE TABLE dbo.Loans (
    LoanId INT IDENTITY(1,1) PRIMARY KEY,
    MemberId INT NOT NULL,
    ProcessedByUserId INT NOT NULL,
    LoanDate DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE),
    DueDate DATE NOT NULL,
    ReturnDate DATE NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Borrowing',
    Note NVARCHAR(300) NULL,
    RenewalCount INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Loans_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(MemberId),
    CONSTRAINT FK_Loans_Users FOREIGN KEY (ProcessedByUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CHK_Loans_Status CHECK (Status IN ('Borrowing', 'Returned', 'Overdue', 'Lost')),
    CONSTRAINT CHK_Loans_Dates CHECK (DueDate >= LoanDate AND (ReturnDate IS NULL OR ReturnDate >= LoanDate))
);

-- 12) Bảng đặt trước đầu sách
CREATE TABLE dbo.BookReservations (
    ReservationId INT IDENTITY(1,1) PRIMARY KEY,
    MemberId INT NOT NULL,
    BookId INT NOT NULL,
    ReservedCopyId INT NULL,
    CreatedByUserId INT NOT NULL,
    FulfilledByUserId INT NULL,
    RequestedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    FulfilledAt DATETIME2 NULL,
    CancelledAt DATETIME2 NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    Note NVARCHAR(300) NULL,
    CONSTRAINT FK_BookReservations_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(MemberId),
    CONSTRAINT FK_BookReservations_Books FOREIGN KEY (BookId) REFERENCES dbo.Books(BookId),
    CONSTRAINT FK_BookReservations_BookCopies FOREIGN KEY (ReservedCopyId) REFERENCES dbo.BookCopies(BookCopyId),
    CONSTRAINT FK_BookReservations_Users_Created FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_BookReservations_Users_Fulfilled FOREIGN KEY (FulfilledByUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CHK_BookReservations_Status CHECK (Status IN ('Pending', 'Ready', 'Fulfilled', 'Cancelled', 'Expired')),
    CONSTRAINT CHK_BookReservations_Flow CHECK (
        (Status = 'Pending' AND ReservedCopyId IS NULL AND FulfilledAt IS NULL AND CancelledAt IS NULL)
        OR (Status = 'Ready' AND ReservedCopyId IS NOT NULL AND FulfilledAt IS NULL AND CancelledAt IS NULL)
        OR (Status = 'Fulfilled' AND FulfilledAt IS NOT NULL)
        OR (Status = 'Cancelled' AND CancelledAt IS NOT NULL)
        OR (Status = 'Expired')
    )
);

-- 13) Bảng chi tiết mượn
CREATE TABLE dbo.LoanItems (
    LoanItemId INT IDENTITY(1,1) PRIMARY KEY,
    LoanId INT NOT NULL,
    BookCopyId INT NOT NULL,
    ConditionBefore NVARCHAR(30) NULL,
    ConditionAfter NVARCHAR(30) NULL,
    ReturnedAt DATETIME2 NULL,
    FineAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_LoanItems_Loans FOREIGN KEY (LoanId) REFERENCES dbo.Loans(LoanId) ON DELETE CASCADE,
    CONSTRAINT FK_LoanItems_BookCopies FOREIGN KEY (BookCopyId) REFERENCES dbo.BookCopies(BookCopyId),
    CONSTRAINT UQ_LoanItems_Loan_BookCopy UNIQUE (LoanId, BookCopyId),
    CONSTRAINT CHK_LoanItems_ConditionBefore CHECK (ConditionBefore IS NULL OR ConditionBefore IN (N'New', N'Good', N'Worn', N'Damaged', N'Lost')),
    CONSTRAINT CHK_LoanItems_ConditionAfter CHECK (ConditionAfter IS NULL OR ConditionAfter IN (N'New', N'Good', N'Worn', N'Damaged', N'Lost')),
    CONSTRAINT CHK_LoanItems_FineAmount CHECK (FineAmount >= 0)
);

-- 14) Bảng thu tiền phạt
CREATE TABLE dbo.FinePayments (
    PaymentId INT IDENTITY(1,1) PRIMARY KEY,
    MemberId INT NOT NULL,
    LoanId INT NULL,
    AmountPaid DECIMAL(12,2) NOT NULL,
    PaymentDate DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    PaymentMethod VARCHAR(20) NULL,
    ReceivedByUserId INT NULL,
    Note NVARCHAR(300) NULL,
    CONSTRAINT FK_FinePayments_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(MemberId),
    CONSTRAINT FK_FinePayments_Loans FOREIGN KEY (LoanId) REFERENCES dbo.Loans(LoanId),
    CONSTRAINT FK_FinePayments_Users FOREIGN KEY (ReceivedByUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CHK_FinePayments_Amount CHECK (AmountPaid > 0)
);

-- 15) Bảng nhật ký hệ thống
CREATE TABLE dbo.SystemLogs (
    LogId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NULL,
    ActionType VARCHAR(50) NOT NULL,
    EntityName VARCHAR(50) NOT NULL,
    EntityId VARCHAR(50) NULL,
    Description NVARCHAR(1000) NULL,
    OldData NVARCHAR(MAX) NULL,
    NewData NVARCHAR(MAX) NULL,
    IpAddress VARCHAR(45) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_SystemLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);

-- Seed cứng: Roles
INSERT INTO dbo.Roles (RoleName, Description) VALUES
('staff', 'Library staff'),
('manager', 'Library manager');

-- Seed cứng: Users (PasswordHash = SHA256('123456'))
INSERT INTO dbo.Users (Username, PasswordHash, FullName, Email, Phone, AvatarUrl, IsActive, RoleId) VALUES
('staff01', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Nguyễn Văn Staff', 'son0352261@gmail.com', '0901000001', '/images/avatar/staff.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='staff')),
('staff02', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Phạm Hải An', 'staff02@library.local', '0901000003', '/images/avatar/staff.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='staff')),
('staff03', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Lê Thanh Bình', 'staff03@library.local', '0901000004', '/images/avatar/staff.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='staff')),
('staff04', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Ngô Gia Minh', 'staff04@library.local', '0901000005', '/images/avatar/staff.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='staff')),
('staff05', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Trần Quốc Việt', 'staff05@library.local', '0901000006', '/images/avatar/staff.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='staff')),
('staff06', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Đỗ Ngọc Lan', 'staff06@library.local', '0901000007', '/images/avatar/staff.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='staff')),
('staff07', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Bùi Minh Thảo', 'staff07@library.local', '0901000008', '/images/avatar/staff.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='staff')),
('staff08', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Hoàng Kim Anh', 'staff08@library.local', '0901000009', '/images/avatar/staff.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='staff')),
('staff09', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Vũ Thanh Tùng', 'staff09@library.local', '0901000010', '/images/avatar/staff.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='staff')),
('staff10', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Nguyễn Thu Hà', 'staff10@library.local', '0901000011', '/images/avatar/staff.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='staff')),
('staff11', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Phan Nhật Khánh', 'staff11@library.local', '0901000012', '/images/avatar/staff.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='staff')),
('manager01', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Trần Thị Manager', 'thaison28092004@gmail.com', '0901000013', '/images/avatar/manager.png', 1, (SELECT RoleId FROM dbo.Roles WHERE RoleName='manager'));

-- Seed cứng: Thông báo mẫu
DECLARE @ManagerUserId INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Username = 'manager01');
DECLARE @Staff01UserId INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Username = 'staff01');
DECLARE @Staff02UserId INT = (SELECT TOP 1 UserId FROM dbo.Users WHERE Username = 'staff02');

INSERT INTO dbo.Notifications (Title, Content, CreatedByUserId, SendToAll)
VALUES (N'Thông báo vận hành tuần', N'Tuần này thư viện cập nhật lịch trực quầy mượn trả vào 08:00 sáng thứ Hai. Vui lòng kiểm tra phân công và xác nhận trước 17:00 Chủ Nhật.', @ManagerUserId, 1);

DECLARE @BroadcastNotificationId INT = SCOPE_IDENTITY();

INSERT INTO dbo.NotificationRecipients (NotificationId, RecipientUserId)
SELECT @BroadcastNotificationId, u.UserId
FROM dbo.Users u
WHERE u.IsActive = 1;

INSERT INTO dbo.Notifications (Title, Content, CreatedByUserId, SendToAll)
VALUES (N'Nhắc riêng: rà soát sách quá hạn', N'Bạn hỗ trợ rà soát danh sách mượn quá hạn của nhóm MEM001-MEM020 và phản hồi trước 15:00 hôm nay.', @ManagerUserId, 0);

DECLARE @PrivateNotificationId INT = SCOPE_IDENTITY();

INSERT INTO dbo.NotificationRecipients (NotificationId, RecipientUserId)
VALUES
(@PrivateNotificationId, @Staff01UserId),
(@PrivateNotificationId, @Staff02UserId);

-- Seed cứng: Members
INSERT INTO dbo.Members (MemberCode, FullName, DateOfBirth, Gender, Email, Phone, AddressLine, IsActive) VALUES
('MEM001', N'Lê Minh Anh', '2001-02-15', 'Female', 'member1@library.local', '0911000001', N'Hà Nội', 1),
('MEM002', N'Nguyễn Quang Huy', '1999-09-20', 'Male', 'member2@library.local', '0911000002', N'TP. HCM', 1),
('MEM003', N'Trần Mỹ Linh', '2002-11-10', 'Female', 'member3@library.local', '0911000003', N'Đà Nẵng', 1),
('MEM004', N'Phạm Gia Huy', '2000-04-18', 'Male', 'member4@library.local', '0911000004', N'Cần Thơ', 1),
('MEM005', N'Đỗ Thu Trang', '2001-12-03', 'Female', 'member5@library.local', '0911000005', N'Hải Phòng', 1),
('MEM006', N'Ngô Minh Quân', '1998-06-25', 'Male', 'member6@library.local', '0911000006', N'Huế', 1),
('MEM007', N'Bùi Hà Chi', '2003-07-19', 'Female', 'member7@library.local', '0911000007', N'Quảng Ninh', 1),
('MEM008', N'Phan Đức Nam', '1997-10-02', 'Male', 'member8@library.local', '0911000008', N'Nghệ An', 1),
('MEM009', N'Trịnh Yến Nhi', '2002-01-11', 'Female', 'member9@library.local', '0911000009', N'Bình Dương', 1),
('MEM010', N'Lý Gia Hân', '2001-03-30', 'Other', 'member10@library.local', '0911000010', N'Khánh Hòa', 1),
('MEM011', N'Hoàng Minh Khang', '2000-08-14', 'Male', 'member11@library.local', '0911000011', N'Lâm Đồng', 1),
('MEM012', N'Vũ Thảo Vy', '2002-05-22', 'Female', 'member12@library.local', '0911000012', N'Bắc Ninh', 1);

-- Seed cứng: danh mục sách Fahasa
DECLARE @BookSeed TABLE (
    ISBN VARCHAR(20) PRIMARY KEY,
    Title NVARCHAR(250) NOT NULL,
    AuthorName NVARCHAR(150) NOT NULL,
    PublisherName NVARCHAR(150) NOT NULL,
    PublishedYear INT NOT NULL,
    CategoryName NVARCHAR(100) NOT NULL,
    ImageUrl VARCHAR(500) NOT NULL
);

INSERT INTO @BookSeed (ISBN, Title, AuthorName, PublisherName, PublishedYear, CategoryName, ImageUrl)
VALUES
    ('8935235247857', N'Một Thoáng Ta Rực Rỡ Ở Nhân Gian (Tái Bản 2026)', N'Ocean Vuong', N'Hội Nhà Văn', 2026, N'Tiểu thuyết', '/images/books/mot-thoang-ta-ruc-ro-o-nhan-gian.jpg'),
    ('9786042399821', N'Tủ Sách Thanh Niên - Dưới Đám Mây Màu Cánh Vạc', N'Thu Bồn', N'Kim Đồng', 2026, N'Tiểu thuyết', '/images/books/duoi-dam-may-mau-canh-vac.jpg'),
    ('8935325035227', N'Dạ Khúc Hồi Tưởng', N'Shichiri Nakayama', N'Dân Trí', 2026, N'Trinh thám', '/images/books/da-khuc-hoi-tuong.jpg'),
    ('9786326231113', N'Không Ngủ Ở Saint Petersburg', N'Trương Anh Ngọc', N'Phụ Nữ Việt Nam', 2025, N'Du ký', '/images/books/khong-ngu-o-saint-petersburg.jpg'),
    ('8935230011040', N'Danh Tác Việt Nam - Truyện Ngắn Nam Cao', N'Nam Cao', N'Văn Học', 2026, N'Truyện ngắn', '/images/books/truyen-ngan-nam-cao.jpg'),
    ('8935235247147', N'Sự Im Lặng Của Bầy Cừu (Tái Bản 2025)', N'Thomas Harris', N'Văn Học', 2025, N'Trinh thám', '/images/books/su-im-lang-cua-bay-cuu.jpg'),
    ('8935325033421', N'Thần Rừng', N'Liz Moore', N'Dân Trí', 2026, N'Trinh thám', '/images/books/than-rung.jpg'),
    ('8934974179672', N'Harry Potter Và Hòn Đá Phù Thủy - Tập 1 (Tái Bản)', N'J.K. Rowling, Lý Lan', N'Trẻ', 2022, N'Fantasy', '/images/books/harry-potter-1.jpg'),
    ('8934974182290', N'Harry Potter Và Phòng Chứa Bí Mật - Tập 2 (Tái Bản 2022)', N'J.K. Rowling', N'Trẻ', 2022, N'Fantasy', '/images/books/harry-potter-2.jpg'),
    ('8934974179658', N'Harry Potter Và Tên Tù Nhân Ngục Azkaban - Tập 3 (Tái Bản)', N'J.K. Rowling, Lý Lan', N'Trẻ', 2022, N'Fantasy', '/images/books/harry-potter-3.jpg'),
    ('8935212322959', N'Những Vụ Kỳ Án Của Sherlock Holmes (TB)', N'Conan Doyle', N'Văn Học', 2015, N'Trinh thám', '/images/books/nhung-vu-ky-an-sherlock.jpg'),
    ('9786043720143', N'Sherlock Holmes - Bài Toán Tại Cầu Thor', N'Sir Arthur Conan Doyle', N'Văn Học', 2022, N'Trinh thám', '/images/books/sherlock-bai-toan-cau-thor.jpg'),
    ('8935325015137', N'Như Sao Trời Ôm Lấy Đại Dương', N'hngoc', N'Dân Trí', 2023, N'Thơ', '/images/books/nhu-sao-troi-om-lay-dai-duong.jpg'),
    ('8935095632053', N'Nhật Ký Trong Tù (Tái Bản)', N'Hồ Chí Minh', N'NXB Văn Học', 2021, N'Thơ', '/images/books/nhat-ky-trong-tu.jpg'),
    ('8935095633586', N'Thơ Xuân Diệu (Tái Bản 2023)', N'Xuân Diệu', N'Văn Học', 2023, N'Thơ', '/images/books/tho-xuan-dieu.jpg'),
    ('8935095618835', N'Truyện Kiều', N'Nguyễn Du, Đào Duy Anh', N'NXB Văn Học', 2015, N'Thơ', '/images/books/truyen-kieu.jpg'),
    ('8935325004469', N'Đi Vòng Thế Giới Vẫn Quanh Một Người', N'Lam', N'NXB Phụ Nữ Việt Nam', 2022, N'Thơ', '/images/books/di-vong-the-gioi-van-quanh-mot-nguoi.jpg'),
    ('8935343700923', N'Tác Phẩm Văn Học Trong Nhà Trường - Thơ Xuân Diệu', N'Xuân Diệu', N'Văn Học', 2023, N'Thơ', '/images/books/tho-xuan-dieu-trong-nha-truong.jpg'),
    ('8935075959187', N'Mưa Đỏ', N'Chu Lai', N'Quân Đội Nhân Dân', 2025, N'Tiểu thuyết', '/images/books/mua-do.jpg'),
    ('8934974187639', N'Cho Tôi Xin Một Vé Đi Tuổi Thơ (Tái Bản 2023)', N'Nguyễn Nhật Ánh', N'Trẻ', 2023, N'Tiểu thuyết', '/images/books/cho-toi-xin-mot-ve-di-tuoi-tho.jpg'),
    ('8934974187622', N'Tôi Thấy Hoa Vàng Trên Cỏ Xanh (Tái Bản 2023)', N'Nguyễn Nhật Ánh', N'Trẻ', 2023, N'Tiểu thuyết', '/images/books/toi-thay-hoa-vang-tren-co-xanh.jpg');

-- Seed cứng: Authors/Categories/Publishers từ dữ liệu cứng
INSERT INTO dbo.Authors (AuthorName, Nationality)
SELECT DISTINCT AuthorName, N'Unknown' FROM @BookSeed;

INSERT INTO dbo.Categories (CategoryName, Description)
SELECT DISTINCT CategoryName, N'Thể loại sách Fahasa' FROM @BookSeed;

INSERT INTO dbo.Publishers (PublisherName, AddressLine)
SELECT DISTINCT PublisherName, N'Việt Nam' FROM @BookSeed;

INSERT INTO dbo.Books (ISBN, Title, ImageUrl, PublisherId, PublishedYear)
SELECT bs.ISBN, bs.Title, bs.ImageUrl, p.PublisherId, bs.PublishedYear
FROM @BookSeed bs
JOIN dbo.Publishers p ON p.PublisherName = bs.PublisherName;

INSERT INTO dbo.BookAuthors (BookId, AuthorId)
SELECT b.BookId, a.AuthorId
FROM @BookSeed bs
JOIN dbo.Books b ON b.ISBN = bs.ISBN
JOIN dbo.Authors a ON a.AuthorName = bs.AuthorName;

INSERT INTO dbo.BookCategories (BookId, CategoryId)
SELECT b.BookId, c.CategoryId
FROM @BookSeed bs
JOIN dbo.Books b ON b.ISBN = bs.ISBN
JOIN dbo.Categories c ON c.CategoryName = bs.CategoryName;

-- Bổ sung thể loại và gán thêm cho một số sách theo yêu cầu nghiệp vụ
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Hành động')
    INSERT INTO dbo.Categories (CategoryName, Description) VALUES (N'Hành động', N'Hành động');

IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Truyện ngắn')
    INSERT INTO dbo.Categories (CategoryName, Description) VALUES (N'Truyện ngắn', N'Truyện ngắn');

IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Kinh dị')
    INSERT INTO dbo.Categories (CategoryName, Description) VALUES (N'Kinh dị', N'Kinh dị');

DECLARE @ActionCategoryId INT = (SELECT TOP 1 CategoryId FROM dbo.Categories WHERE CategoryName = N'Hành động');
DECLARE @ShortStoryCategoryId INT = (SELECT TOP 1 CategoryId FROM dbo.Categories WHERE CategoryName = N'Truyện ngắn');
DECLARE @HorrorCategoryId INT = (SELECT TOP 1 CategoryId FROM dbo.Categories WHERE CategoryName = N'Kinh dị');

DECLARE @SherlockBook1Id INT = (SELECT TOP 1 BookId FROM dbo.Books WHERE ISBN = '8935212322959');
DECLARE @SherlockBook2Id INT = (SELECT TOP 1 BookId FROM dbo.Books WHERE ISBN = '9786043720143');
DECLARE @MuaDoBookId INT = (SELECT TOP 1 BookId FROM dbo.Books WHERE ISBN = '8935075959187');
DECLARE @ChoToiBookId INT = (SELECT TOP 1 BookId FROM dbo.Books WHERE ISBN = '8934974187639');
DECLARE @DaKhucBookId INT = (SELECT TOP 1 BookId FROM dbo.Books WHERE ISBN = '8935325035227');

IF @SherlockBook1Id IS NOT NULL AND @ActionCategoryId IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM dbo.BookCategories WHERE BookId = @SherlockBook1Id AND CategoryId = @ActionCategoryId)
    INSERT INTO dbo.BookCategories (BookId, CategoryId) VALUES (@SherlockBook1Id, @ActionCategoryId);

IF @SherlockBook2Id IS NOT NULL AND @ActionCategoryId IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM dbo.BookCategories WHERE BookId = @SherlockBook2Id AND CategoryId = @ActionCategoryId)
    INSERT INTO dbo.BookCategories (BookId, CategoryId) VALUES (@SherlockBook2Id, @ActionCategoryId);

IF @MuaDoBookId IS NOT NULL AND @ActionCategoryId IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM dbo.BookCategories WHERE BookId = @MuaDoBookId AND CategoryId = @ActionCategoryId)
    INSERT INTO dbo.BookCategories (BookId, CategoryId) VALUES (@MuaDoBookId, @ActionCategoryId);

IF @ChoToiBookId IS NOT NULL AND @ShortStoryCategoryId IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM dbo.BookCategories WHERE BookId = @ChoToiBookId AND CategoryId = @ShortStoryCategoryId)
    INSERT INTO dbo.BookCategories (BookId, CategoryId) VALUES (@ChoToiBookId, @ShortStoryCategoryId);

IF @DaKhucBookId IS NOT NULL AND @HorrorCategoryId IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM dbo.BookCategories WHERE BookId = @DaKhucBookId AND CategoryId = @HorrorCategoryId)
    INSERT INTO dbo.BookCategories (BookId, CategoryId) VALUES (@DaKhucBookId, @HorrorCategoryId);

-- Seed cứng: BookCopies (10 bản sao mỗi ISBN, Barcode 5 số)
INSERT INTO dbo.BookCopies (BookId, Barcode, AcquiredDate, CopyStatus, PhysicalCondition, LocationCode, IsActive)
SELECT
    b.BookId,
    v.Barcode,
    CAST(v.AcquiredDate AS DATE),
    v.CopyStatus,
    v.PhysicalCondition,
    NULL,
    v.IsActive
FROM (VALUES
    ('8935235247857', '00001', '2025-01-01', 'Available', N'New', 1),
    ('8935235247857', '00002', '2025-01-01', 'Available', N'New', 1),
    ('8935235247857', '00003', '2025-01-01', 'Available', N'New', 1),
    ('8935235247857', '00004', '2025-01-01', 'Available', N'New', 1),
    ('8935235247857', '00005', '2025-01-01', 'Available', N'New', 1),
    ('8935235247857', '00006', '2025-01-01', 'Available', N'New', 1),
    ('8935235247857', '00007', '2025-01-01', 'Available', N'New', 1),
    ('8935235247857', '00008', '2025-01-01', 'Available', N'New', 1),
    ('8935235247857', '00009', '2025-01-01', 'Available', N'New', 1),
    ('8935235247857', '00010', '2025-01-01', 'Available', N'New', 1),
    ('9786042399821', '00011', '2025-01-01', 'Available', N'New', 1),
    ('9786042399821', '00012', '2025-01-01', 'Available', N'New', 1),
    ('9786042399821', '00013', '2025-01-01', 'Available', N'New', 1),
    ('9786042399821', '00014', '2025-01-01', 'Available', N'New', 1),
    ('9786042399821', '00015', '2025-01-01', 'Available', N'New', 1),
    ('9786042399821', '00016', '2025-01-01', 'Available', N'New', 1),
    ('9786042399821', '00017', '2025-01-01', 'Available', N'New', 1),
    ('9786042399821', '00018', '2025-01-01', 'Available', N'New', 1),
    ('9786042399821', '00019', '2025-01-01', 'Available', N'New', 1),
    ('9786042399821', '00020', '2025-01-01', 'Available', N'New', 1),
    ('8935325035227', '00021', '2025-01-01', 'Available', N'New', 1),
    ('8935325035227', '00022', '2025-01-01', 'Available', N'New', 1),
    ('8935325035227', '00023', '2025-01-01', 'Available', N'New', 1),
    ('8935325035227', '00024', '2025-01-01', 'Available', N'New', 1),
    ('8935325035227', '00025', '2025-01-01', 'Available', N'New', 1),
    ('8935325035227', '00026', '2025-01-01', 'Available', N'New', 1),
    ('8935325035227', '00027', '2025-01-01', 'Available', N'New', 1),
    ('8935325035227', '00028', '2025-01-01', 'Available', N'New', 1),
    ('8935325035227', '00029', '2025-01-01', 'Available', N'New', 1),
    ('8935325035227', '00030', '2025-01-01', 'Available', N'New', 1),
    ('9786326231113', '00031', '2025-01-01', 'Available', N'New', 1),
    ('9786326231113', '00032', '2025-01-01', 'Available', N'New', 1),
    ('9786326231113', '00033', '2025-01-01', 'Available', N'New', 1),
    ('9786326231113', '00034', '2025-01-01', 'Available', N'New', 1),
    ('9786326231113', '00035', '2025-01-01', 'Available', N'New', 1),
    ('9786326231113', '00036', '2025-01-01', 'Available', N'New', 1),
    ('9786326231113', '00037', '2025-01-01', 'Available', N'New', 1),
    ('9786326231113', '00038', '2025-01-01', 'Available', N'New', 1),
    ('9786326231113', '00039', '2025-01-01', 'Available', N'New', 1),
    ('9786326231113', '00040', '2025-01-01', 'Available', N'New', 1),
    ('8935230011040', '00041', '2025-01-01', 'Available', N'New', 1),
    ('8935230011040', '00042', '2025-01-01', 'Available', N'New', 1),
    ('8935230011040', '00043', '2025-01-01', 'Available', N'New', 1),
    ('8935230011040', '00044', '2025-01-01', 'Available', N'New', 1),
    ('8935230011040', '00045', '2025-01-01', 'Available', N'New', 1),
    ('8935230011040', '00046', '2025-01-01', 'Available', N'New', 1),
    ('8935230011040', '00047', '2025-01-01', 'Available', N'New', 1),
    ('8935230011040', '00048', '2025-01-01', 'Available', N'New', 1),
    ('8935230011040', '00049', '2025-01-01', 'Available', N'New', 1),
    ('8935230011040', '00050', '2025-01-01', 'Available', N'New', 1),
    ('8935235247147', '00051', '2025-01-01', 'Available', N'New', 1),
    ('8935235247147', '00052', '2025-01-01', 'Available', N'New', 1),
    ('8935235247147', '00053', '2025-01-01', 'Available', N'New', 1),
    ('8935235247147', '00054', '2025-01-01', 'Available', N'New', 1),
    ('8935235247147', '00055', '2025-01-01', 'Available', N'New', 1),
    ('8935235247147', '00056', '2025-01-01', 'Available', N'New', 1),
    ('8935235247147', '00057', '2025-01-01', 'Available', N'New', 1),
    ('8935235247147', '00058', '2025-01-01', 'Available', N'New', 1),
    ('8935235247147', '00059', '2025-01-01', 'Available', N'New', 1),
    ('8935235247147', '00060', '2025-01-01', 'Available', N'New', 1),
    ('8935325033421', '00061', '2025-01-01', 'Available', N'New', 1),
    ('8935325033421', '00062', '2025-01-01', 'Available', N'New', 1),
    ('8935325033421', '00063', '2025-01-01', 'Available', N'New', 1),
    ('8935325033421', '00064', '2025-01-01', 'Available', N'New', 1),
    ('8935325033421', '00065', '2025-01-01', 'Available', N'New', 1),
    ('8935325033421', '00066', '2025-01-01', 'Available', N'New', 1),
    ('8935325033421', '00067', '2025-01-01', 'Available', N'New', 1),
    ('8935325033421', '00068', '2025-01-01', 'Available', N'New', 1),
    ('8935325033421', '00069', '2025-01-01', 'Available', N'New', 1),
    ('8935325033421', '00070', '2025-01-01', 'Available', N'New', 1),
    ('8934974179672', '00071', '2025-01-01', 'Available', N'New', 1),
    ('8934974179672', '00072', '2025-01-01', 'Available', N'New', 1),
    ('8934974179672', '00073', '2025-01-01', 'Available', N'New', 1),
    ('8934974179672', '00074', '2025-01-01', 'Available', N'New', 1),
    ('8934974179672', '00075', '2025-01-01', 'Available', N'New', 1),
    ('8934974179672', '00076', '2025-01-01', 'Available', N'New', 1),
    ('8934974179672', '00077', '2025-01-01', 'Available', N'New', 1),
    ('8934974179672', '00078', '2025-01-01', 'Available', N'New', 1),
    ('8934974179672', '00079', '2025-01-01', 'Available', N'New', 1),
    ('8934974179672', '00080', '2025-01-01', 'Available', N'New', 1),
    ('8934974182290', '00081', '2025-01-01', 'Available', N'New', 1),
    ('8934974182290', '00082', '2025-01-01', 'Available', N'New', 1),
    ('8934974182290', '00083', '2025-01-01', 'Available', N'New', 1),
    ('8934974182290', '00084', '2025-01-01', 'Available', N'New', 1),
    ('8934974182290', '00085', '2025-01-01', 'Available', N'New', 1),
    ('8934974182290', '00086', '2025-01-01', 'Available', N'New', 1),
    ('8934974182290', '00087', '2025-01-01', 'Available', N'New', 1),
    ('8934974182290', '00088', '2025-01-01', 'Available', N'New', 1),
    ('8934974182290', '00089', '2025-01-01', 'Available', N'New', 1),
    ('8934974182290', '00090', '2025-01-01', 'Available', N'New', 1),
    ('8934974179658', '00091', '2025-01-01', 'Available', N'New', 1),
    ('8934974179658', '00092', '2025-01-01', 'Available', N'New', 1),
    ('8934974179658', '00093', '2025-01-01', 'Available', N'New', 1),
    ('8934974179658', '00094', '2025-01-01', 'Available', N'New', 1),
    ('8934974179658', '00095', '2025-01-01', 'Available', N'New', 1),
    ('8934974179658', '00096', '2025-01-01', 'Available', N'New', 1),
    ('8934974179658', '00097', '2025-01-01', 'Available', N'New', 1),
    ('8934974179658', '00098', '2025-01-01', 'Available', N'New', 1),
    ('8934974179658', '00099', '2025-01-01', 'Available', N'New', 1),
    ('8934974179658', '00100', '2025-01-01', 'Available', N'New', 1),
    ('8935212322959', '00101', '2025-01-01', 'Available', N'New', 1),
    ('8935212322959', '00102', '2025-01-01', 'Available', N'New', 1),
    ('8935212322959', '00103', '2025-01-01', 'Available', N'New', 1),
    ('8935212322959', '00104', '2025-01-01', 'Available', N'New', 1),
    ('8935212322959', '00105', '2025-01-01', 'Available', N'New', 1),
    ('8935212322959', '00106', '2025-01-01', 'Available', N'New', 1),
    ('8935212322959', '00107', '2025-01-01', 'Available', N'New', 1),
    ('8935212322959', '00108', '2025-01-01', 'Available', N'New', 1),
    ('8935212322959', '00109', '2025-01-01', 'Available', N'New', 1),
    ('8935212322959', '00110', '2025-01-01', 'Available', N'New', 1),
    ('9786043720143', '00111', '2025-01-01', 'Available', N'New', 1),
    ('9786043720143', '00112', '2025-01-01', 'Available', N'New', 1),
    ('9786043720143', '00113', '2025-01-01', 'Available', N'New', 1),
    ('9786043720143', '00114', '2025-01-01', 'Available', N'New', 1),
    ('9786043720143', '00115', '2025-01-01', 'Available', N'New', 1),
    ('9786043720143', '00116', '2025-01-01', 'Available', N'New', 1),
    ('9786043720143', '00117', '2025-01-01', 'Available', N'New', 1),
    ('9786043720143', '00118', '2025-01-01', 'Available', N'New', 1),
    ('9786043720143', '00119', '2025-01-01', 'Available', N'New', 1),
    ('9786043720143', '00120', '2025-01-01', 'Available', N'New', 1),
    ('8935325015137', '00121', '2025-01-01', 'Available', N'New', 1),
    ('8935325015137', '00122', '2025-01-01', 'Available', N'New', 1),
    ('8935325015137', '00123', '2025-01-01', 'Available', N'New', 1),
    ('8935325015137', '00124', '2025-01-01', 'Available', N'New', 1),
    ('8935325015137', '00125', '2025-01-01', 'Available', N'New', 1),
    ('8935325015137', '00126', '2025-01-01', 'Available', N'New', 1),
    ('8935325015137', '00127', '2025-01-01', 'Available', N'New', 1),
    ('8935325015137', '00128', '2025-01-01', 'Available', N'New', 1),
    ('8935325015137', '00129', '2025-01-01', 'Available', N'New', 1),
    ('8935325015137', '00130', '2025-01-01', 'Available', N'New', 1),
    ('8935095632053', '00131', '2025-01-01', 'Available', N'New', 1),
    ('8935095632053', '00132', '2025-01-01', 'Available', N'New', 1),
    ('8935095632053', '00133', '2025-01-01', 'Available', N'New', 1),
    ('8935095632053', '00134', '2025-01-01', 'Available', N'New', 1),
    ('8935095632053', '00135', '2025-01-01', 'Available', N'New', 1),
    ('8935095632053', '00136', '2025-01-01', 'Available', N'New', 1),
    ('8935095632053', '00137', '2025-01-01', 'Available', N'New', 1),
    ('8935095632053', '00138', '2025-01-01', 'Available', N'New', 1),
    ('8935095632053', '00139', '2025-01-01', 'Available', N'New', 1),
    ('8935095632053', '00140', '2025-01-01', 'Available', N'New', 1),
    ('8935095633586', '00141', '2025-01-01', 'Available', N'New', 1),
    ('8935095633586', '00142', '2025-01-01', 'Available', N'New', 1),
    ('8935095633586', '00143', '2025-01-01', 'Available', N'New', 1),
    ('8935095633586', '00144', '2025-01-01', 'Available', N'New', 1),
    ('8935095633586', '00145', '2025-01-01', 'Available', N'New', 1),
    ('8935095633586', '00146', '2025-01-01', 'Available', N'New', 1),
    ('8935095633586', '00147', '2025-01-01', 'Available', N'New', 1),
    ('8935095633586', '00148', '2025-01-01', 'Available', N'New', 1),
    ('8935095633586', '00149', '2025-01-01', 'Available', N'New', 1),
    ('8935095633586', '00150', '2025-01-01', 'Available', N'New', 1),
    ('8935095618835', '00151', '2025-01-01', 'Available', N'New', 1),
    ('8935095618835', '00152', '2025-01-01', 'Available', N'New', 1),
    ('8935095618835', '00153', '2025-01-01', 'Available', N'New', 1),
    ('8935095618835', '00154', '2025-01-01', 'Available', N'New', 1),
    ('8935095618835', '00155', '2025-01-01', 'Available', N'New', 1),
    ('8935095618835', '00156', '2025-01-01', 'Available', N'New', 1),
    ('8935095618835', '00157', '2025-01-01', 'Available', N'New', 1),
    ('8935095618835', '00158', '2025-01-01', 'Available', N'New', 1),
    ('8935095618835', '00159', '2025-01-01', 'Available', N'New', 1),
    ('8935095618835', '00160', '2025-01-01', 'Available', N'New', 1),
    ('8935325004469', '00161', '2025-01-01', 'Available', N'New', 1),
    ('8935325004469', '00162', '2025-01-01', 'Available', N'New', 1),
    ('8935325004469', '00163', '2025-01-01', 'Available', N'New', 1),
    ('8935325004469', '00164', '2025-01-01', 'Available', N'New', 1),
    ('8935325004469', '00165', '2025-01-01', 'Available', N'New', 1),
    ('8935325004469', '00166', '2025-01-01', 'Available', N'New', 1),
    ('8935325004469', '00167', '2025-01-01', 'Available', N'New', 1),
    ('8935325004469', '00168', '2025-01-01', 'Available', N'New', 1),
    ('8935325004469', '00169', '2025-01-01', 'Available', N'New', 1),
    ('8935325004469', '00170', '2025-01-01', 'Available', N'New', 1),
    ('8935343700923', '00171', '2025-01-01', 'Available', N'New', 1),
    ('8935343700923', '00172', '2025-01-01', 'Available', N'New', 1),
    ('8935343700923', '00173', '2025-01-01', 'Available', N'New', 1),
    ('8935343700923', '00174', '2025-01-01', 'Available', N'New', 1),
    ('8935343700923', '00175', '2025-01-01', 'Available', N'New', 1),
    ('8935343700923', '00176', '2025-01-01', 'Available', N'New', 1),
    ('8935343700923', '00177', '2025-01-01', 'Available', N'New', 1),
    ('8935343700923', '00178', '2025-01-01', 'Available', N'New', 1),
    ('8935343700923', '00179', '2025-01-01', 'Available', N'New', 1),
    ('8935343700923', '00180', '2025-01-01', 'Available', N'New', 1),
    ('8935075959187', '00181', '2025-01-01', 'Available', N'New', 1),
    ('8935075959187', '00182', '2025-01-01', 'Available', N'New', 1),
    ('8935075959187', '00183', '2025-01-01', 'Available', N'New', 1),
    ('8935075959187', '00184', '2025-01-01', 'Available', N'New', 1),
    ('8935075959187', '00185', '2025-01-01', 'Available', N'New', 1),
    ('8935075959187', '00186', '2025-01-01', 'Available', N'New', 1),
    ('8935075959187', '00187', '2025-01-01', 'Available', N'New', 1),
    ('8935075959187', '00188', '2025-01-01', 'Available', N'New', 1),
    ('8935075959187', '00189', '2025-01-01', 'Available', N'New', 1),
    ('8935075959187', '00190', '2025-01-01', 'Available', N'New', 1),
    ('8934974187639', '00191', '2025-01-01', 'Available', N'New', 1),
    ('8934974187639', '00192', '2025-01-01', 'Available', N'New', 1),
    ('8934974187639', '00193', '2025-01-01', 'Available', N'New', 1),
    ('8934974187639', '00194', '2025-01-01', 'Available', N'New', 1),
    ('8934974187639', '00195', '2025-01-01', 'Available', N'New', 1),
    ('8934974187639', '00196', '2025-01-01', 'Available', N'New', 1),
    ('8934974187639', '00197', '2025-01-01', 'Available', N'New', 1),
    ('8934974187639', '00198', '2025-01-01', 'Available', N'New', 1),
    ('8934974187639', '00199', '2025-01-01', 'Available', N'New', 1),
    ('8934974187639', '00200', '2025-01-01', 'Available', N'New', 1),
    ('8934974187622', '00201', '2025-01-01', 'Available', N'New', 1),
    ('8934974187622', '00202', '2025-01-01', 'Available', N'New', 1),
    ('8934974187622', '00203', '2025-01-01', 'Available', N'New', 1),
    ('8934974187622', '00204', '2025-01-01', 'Available', N'New', 1),
    ('8934974187622', '00205', '2025-01-01', 'Available', N'New', 1),
    ('8934974187622', '00206', '2025-01-01', 'Available', N'New', 1),
    ('8934974187622', '00207', '2025-01-01', 'Available', N'New', 1),
    ('8934974187622', '00208', '2025-01-01', 'Available', N'New', 1),
    ('8934974187622', '00209', '2025-01-01', 'Available', N'New', 1),
    ('8934974187622', '00210', '2025-01-01', 'Available', N'New', 1)
) AS v(ISBN, Barcode, AcquiredDate, CopyStatus, PhysicalCondition, IsActive)
JOIN dbo.Books b ON b.ISBN = v.ISBN;
-- Seed cứng: Loans + LoanItems
DECLARE @SeedLoanIds TABLE (
    SeedSeq INT IDENTITY(1,1) PRIMARY KEY,
    LoanId INT NOT NULL
);

INSERT INTO dbo.Loans (MemberId, ProcessedByUserId, LoanDate, DueDate, ReturnDate, Status)
OUTPUT inserted.LoanId INTO @SeedLoanIds(LoanId)
VALUES
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM001'), (SELECT UserId FROM dbo.Users WHERE Username='staff01'), '2026-03-20', '2026-04-03', NULL, 'Borrowing'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM002'), (SELECT UserId FROM dbo.Users WHERE Username='staff01'), '2026-03-15', '2026-03-29', '2026-03-25', 'Returned'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM004'), (SELECT UserId FROM dbo.Users WHERE Username='staff01'), '2026-03-22', '2026-04-05', NULL, 'Borrowing'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM005'), (SELECT UserId FROM dbo.Users WHERE Username='staff01'), '2026-03-10', '2026-03-20', NULL, 'Overdue'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM003'), (SELECT UserId FROM dbo.Users WHERE Username='staff01'), '2026-03-25', '2026-04-08', NULL, 'Borrowing');

INSERT INTO dbo.LoanItems (LoanId, BookCopyId, ConditionBefore, ConditionAfter, ReturnedAt, FineAmount) VALUES
((SELECT LoanId FROM @SeedLoanIds WHERE SeedSeq = 1), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00001'), N'New', NULL, NULL, 0),
((SELECT LoanId FROM @SeedLoanIds WHERE SeedSeq = 1), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00021'), N'New', NULL, NULL, 0),
((SELECT LoanId FROM @SeedLoanIds WHERE SeedSeq = 2), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00011'), N'Good', N'Good', '2026-03-25 09:00:00', 0),
((SELECT LoanId FROM @SeedLoanIds WHERE SeedSeq = 3), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00201'), N'New', NULL, NULL, 0),
((SELECT LoanId FROM @SeedLoanIds WHERE SeedSeq = 4), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00181'), N'Good', NULL, NULL, 0),
((SELECT LoanId FROM @SeedLoanIds WHERE SeedSeq = 5), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00121'), N'New', NULL, NULL, 0),
((SELECT LoanId FROM @SeedLoanIds WHERE SeedSeq = 5), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00141'), N'New', NULL, NULL, 0);

-- Seed bổ sung: loans/loan-items để dashboard hiển thị rõ doanh thu-phạt theo tháng và công nợ
DECLARE @ExtraLoanIds TABLE (
    SeedSeq INT IDENTITY(1,1) PRIMARY KEY,
    LoanId INT NOT NULL
);

INSERT INTO dbo.Loans (MemberId, ProcessedByUserId, LoanDate, DueDate, ReturnDate, Status, Note)
OUTPUT inserted.LoanId INTO @ExtraLoanIds(LoanId)
VALUES
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM001'), (SELECT UserId FROM dbo.Users WHERE Username='staff02'), '2026-01-05', '2026-01-20', '2026-01-23', 'Returned', N'Trả trễ 3 ngày'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM002'), (SELECT UserId FROM dbo.Users WHERE Username='staff03'), '2026-02-01', '2026-02-15', '2026-02-14', 'Returned', N'Trả đúng hạn'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM003'), (SELECT UserId FROM dbo.Users WHERE Username='staff04'), '2026-02-20', '2026-03-01', '2026-03-08', 'Returned', N'Sách cũ khi trả'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM004'), (SELECT UserId FROM dbo.Users WHERE Username='staff05'), '2026-03-18', '2026-04-01', NULL, 'Overdue', N'Đang quá hạn chưa trả'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM005'), (SELECT UserId FROM dbo.Users WHERE Username='staff06'), '2026-04-05', '2026-04-20', NULL, 'Borrowing', N'Đang mượn bình thường'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM002'), (SELECT UserId FROM dbo.Users WHERE Username='staff07'), '2026-03-01', '2026-03-20', '2026-03-19', 'Returned', N'Sách hư hỏng khi trả'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM003'), (SELECT UserId FROM dbo.Users WHERE Username='staff08'), '2026-03-25', '2026-04-05', NULL, 'Lost', N'Mất sách chưa bồi hoàn đủ'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM006'), (SELECT UserId FROM dbo.Users WHERE Username='staff09'), '2026-04-07', '2026-04-21', NULL, 'Borrowing', N'Mượn nhiều thể loại để test báo cáo'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM007'), (SELECT UserId FROM dbo.Users WHERE Username='staff10'), '2026-04-08', '2026-04-22', NULL, 'Borrowing', N'Mượn sách tâm lý và trinh thám'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM008'), (SELECT UserId FROM dbo.Users WHERE Username='staff11'), '2026-04-09', '2026-04-23', '2026-04-11', 'Returned', N'Trả nhanh trong 2 ngày');

INSERT INTO dbo.LoanItems (LoanId, BookCopyId, ConditionBefore, ConditionAfter, ReturnedAt, FineAmount) VALUES
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 1), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00031'), N'Good', N'Good', '2026-01-23 10:15:00', 6000),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 2), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00041'), N'Good', N'Good', '2026-02-14 16:20:00', 0),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 3), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00051'), N'Good', N'Worn', '2026-03-08 09:30:00', 30000),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 4), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00071'), N'Good', NULL, NULL, 0),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 5), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00081'), N'New', NULL, NULL, 0),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 5), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00111'), N'Good', NULL, NULL, 0),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 6), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00061'), N'Good', N'Damaged', '2026-03-19 11:10:00', 50000),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 7), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00091'), N'Good', N'Lost', NULL, 200000),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 8), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00131'), N'Good', NULL, NULL, 0),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 8), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00151'), N'Good', NULL, NULL, 0),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 9), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00161'), N'Good', NULL, NULL, 0),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 9), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00171'), N'Good', NULL, NULL, 0),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 10), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00191'), N'Good', N'Good', '2026-04-11 14:00:00', 0),
((SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 10), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00202'), N'Good', N'Good', '2026-04-11 14:05:00', 0);

UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed' WHERE Barcode = '00001';
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed' WHERE Barcode = '00021';
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed', PhysicalCondition = N'Good' WHERE Barcode = '00111';
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed', PhysicalCondition = N'Good' WHERE Barcode = '00131';
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed', PhysicalCondition = N'Good' WHERE Barcode = '00151';
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed', PhysicalCondition = N'Good' WHERE Barcode = '00161';
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed', PhysicalCondition = N'Good' WHERE Barcode = '00171';
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed' WHERE Barcode = '00201';
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed' WHERE Barcode = '00181';
UPDATE dbo.BookCopies SET CopyStatus = 'Reserved', PhysicalCondition = N'Good' WHERE Barcode = '00203';
UPDATE dbo.BookCopies SET CopyStatus = 'Maintenance', PhysicalCondition = N'Worn' WHERE Barcode = '00133';
UPDATE dbo.BookCopies SET CopyStatus = 'Disposed', PhysicalCondition = N'Damaged' WHERE Barcode = '00066';

-- Seed đặt trước để kiểm thử ưu tiên khi trả sách
INSERT INTO dbo.BookReservations (MemberId, BookId, ReservedCopyId, CreatedByUserId, RequestedAt, Status, Note)
VALUES
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM009'), (SELECT BookId FROM dbo.Books WHERE ISBN='8934974187622'), NULL, (SELECT UserId FROM dbo.Users WHERE Username='staff01'), '2026-04-09 09:10:00', 'Pending', N'Ưu tiên đầu hàng chờ cho sách tâm lý'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM010'), (SELECT BookId FROM dbo.Books WHERE ISBN='8934974187622'), NULL, (SELECT UserId FROM dbo.Users WHERE Username='staff02'), '2026-04-09 10:30:00', 'Pending', N'Hàng chờ thứ hai cùng đầu sách'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM011'), (SELECT BookId FROM dbo.Books WHERE ISBN='8934974187622'), (SELECT BookCopyId FROM dbo.BookCopies WHERE Barcode='00203'), (SELECT UserId FROM dbo.Users WHERE Username='staff03'), '2026-04-10 08:00:00', 'Ready', N'Đã có bản sao giữ sẵn'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM012'), (SELECT BookId FROM dbo.Books WHERE ISBN='8935325015137'), NULL, (SELECT UserId FROM dbo.Users WHERE Username='staff04'), '2026-04-08 15:45:00', 'Pending', N'Chờ có sách trả để giữ');
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed' WHERE Barcode = '00121';
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed' WHERE Barcode = '00141';
UPDATE dbo.BookCopies SET CopyStatus = 'Available' WHERE Barcode = '00011';
UPDATE dbo.BookCopies SET CopyStatus = 'Available', PhysicalCondition = N'Good' WHERE Barcode = '00031';
UPDATE dbo.BookCopies SET CopyStatus = 'Available', PhysicalCondition = N'Good' WHERE Barcode = '00041';
UPDATE dbo.BookCopies SET CopyStatus = 'Available', PhysicalCondition = N'Worn' WHERE Barcode = '00051';
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed', PhysicalCondition = N'Good' WHERE Barcode = '00071';
UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed', PhysicalCondition = N'New' WHERE Barcode = '00081';
UPDATE dbo.BookCopies SET CopyStatus = 'Damaged', PhysicalCondition = N'Damaged' WHERE Barcode = '00061';
UPDATE dbo.BookCopies SET CopyStatus = 'Lost', PhysicalCondition = N'Lost' WHERE Barcode = '00091';

-- Seed cứng: FinePayments (phục vụ dashboard doanh thu/phạt)
INSERT INTO dbo.FinePayments (MemberId, LoanId, AmountPaid, PaymentDate, PaymentMethod, ReceivedByUserId, Note) VALUES
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM001'), (SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 1), 4000, '2026-01-24 09:00:00', 'cash', (SELECT UserId FROM dbo.Users WHERE Username='staff02'), N'Thu một phần phí quá hạn'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM003'), (SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 3), 10000, '2026-03-10 10:00:00', 'bank', (SELECT UserId FROM dbo.Users WHERE Username='staff04'), N'Thu phí sách cũ'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM002'), (SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 6), 50000, '2026-03-20 15:00:00', 'cash', (SELECT UserId FROM dbo.Users WHERE Username='staff07'), N'Thu đủ phí sách hư hỏng'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM003'), (SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 7), 20000, '2026-04-02 08:45:00', 'card', (SELECT UserId FROM dbo.Users WHERE Username='staff08'), N'Thu một phần phí mất sách'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM001'), (SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 1), 1000, '2026-01-26 14:00:00', 'cash', (SELECT UserId FROM dbo.Users WHERE Username='staff01'), N'Thu bổ sung đợt 2'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM003'), (SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 3), 5000, '2026-03-12 09:20:00', 'bank', (SELECT UserId FROM dbo.Users WHERE Username='staff04'), N'Thu bổ sung phí cũ'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM003'), (SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 7), 15000, '2026-04-05 17:10:00', 'card', (SELECT UserId FROM dbo.Users WHERE Username='staff08'), N'Thu bổ sung mất sách'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM002'), (SELECT LoanId FROM @ExtraLoanIds WHERE SeedSeq = 6), 5000, '2026-03-22 10:35:00', 'cash', (SELECT UserId FROM dbo.Users WHERE Username='staff07'), N'Phụ thu xử lý bìa sách'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM004'), NULL, 12000, '2026-02-03 08:10:00', 'bank', (SELECT UserId FROM dbo.Users WHERE Username='staff03'), N'Thu nợ tồn từ kỳ trước'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM005'), NULL, 7000, '2026-02-14 11:45:00', 'cash', (SELECT UserId FROM dbo.Users WHERE Username='staff05'), N'Thu phí vi phạm nhẹ'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM006'), NULL, 9000, '2026-04-08 09:00:00', 'card', (SELECT UserId FROM dbo.Users WHERE Username='staff06'), N'Thu phí bảo quản sách'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM007'), NULL, 6000, '2026-04-10 16:20:00', 'cash', (SELECT UserId FROM dbo.Users WHERE Username='staff02'), N'Thu phí trả chậm'),
-- Seed doanh thu nhiều tháng để dashboard hiển thị xu hướng lên/xuống rõ ràng
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM008'), NULL, 18000, '2026-01-08 08:40:00', 'cash', (SELECT UserId FROM dbo.Users WHERE Username='staff08'), N'Dữ liệu doanh thu dashboard tháng 01'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM009'), NULL, 12000, '2026-01-18 15:25:00', 'bank', (SELECT UserId FROM dbo.Users WHERE Username='staff09'), N'Dữ liệu doanh thu dashboard tháng 01'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM010'), NULL, 9500, '2026-02-21 10:05:00', 'cash', (SELECT UserId FROM dbo.Users WHERE Username='staff10'), N'Dữ liệu doanh thu dashboard tháng 02'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM011'), NULL, 14000, '2026-03-05 11:30:00', 'card', (SELECT UserId FROM dbo.Users WHERE Username='staff11'), N'Dữ liệu doanh thu dashboard tháng 03'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM012'), NULL, 8500, '2026-04-18 16:45:00', 'bank', (SELECT UserId FROM dbo.Users WHERE Username='staff01'), N'Dữ liệu doanh thu dashboard tháng 04'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM001'), NULL, 42000, '2026-05-06 09:15:00', 'cash', (SELECT UserId FROM dbo.Users WHERE Username='staff02'), N'Dữ liệu doanh thu dashboard tháng 05'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM002'), NULL, 28000, '2026-05-17 14:10:00', 'bank', (SELECT UserId FROM dbo.Users WHERE Username='staff03'), N'Dữ liệu doanh thu dashboard tháng 05'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM003'), NULL, 25000, '2026-05-29 10:55:00', 'card', (SELECT UserId FROM dbo.Users WHERE Username='staff04'), N'Dữ liệu doanh thu dashboard tháng 05'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM004'), NULL, 11000, '2026-06-04 08:20:00', 'cash', (SELECT UserId FROM dbo.Users WHERE Username='staff05'), N'Dữ liệu doanh thu dashboard tháng 06'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM005'), NULL, 15000, '2026-06-23 17:00:00', 'bank', (SELECT UserId FROM dbo.Users WHERE Username='staff06'), N'Dữ liệu doanh thu dashboard tháng 06'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM006'), NULL, 32000, '2026-07-03 09:45:00', 'card', (SELECT UserId FROM dbo.Users WHERE Username='staff07'), N'Dữ liệu doanh thu dashboard tháng 07'),
((SELECT MemberId FROM dbo.Members WHERE MemberCode='MEM007'), NULL, 40000, '2026-07-16 13:35:00', 'cash', (SELECT UserId FROM dbo.Users WHERE Username='staff08'), N'Dữ liệu doanh thu dashboard tháng 07');

-- Seed cứng: SystemLogs
INSERT INTO dbo.SystemLogs (UserId, ActionType, EntityName, EntityId, Description, IpAddress) VALUES
(NULL, 'SEED', 'System', 'INIT', N'Khởi tạo dữ liệu mẫu hệ thống thư viện', '127.0.0.1'),
((SELECT UserId FROM dbo.Users WHERE Username='staff01'), 'LOGIN', 'Users', 'staff01', N'Đăng nhập thành công', '127.0.0.1');

-- Performance and integrity indexes for production-like usage
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE INDEX IX_Users_Role_IsActive ON dbo.Users (RoleId, IsActive);
GO
CREATE UNIQUE INDEX IX_Users_Phone_NotNull ON dbo.Users (Phone) WHERE Phone IS NOT NULL;
GO
CREATE INDEX IX_Members_IsActive ON dbo.Members (IsActive);
GO
CREATE UNIQUE INDEX IX_Members_Phone_NotNull ON dbo.Members (Phone) WHERE Phone IS NOT NULL;
GO
CREATE INDEX IX_Books_IsActive_Title ON dbo.Books (IsActive, Title);
GO
CREATE INDEX IX_BookCopies_Book_Status_IsActive ON dbo.BookCopies (BookId, CopyStatus, IsActive);
GO
CREATE INDEX IX_BookReservations_Book_Status_RequestedAt ON dbo.BookReservations (BookId, Status, RequestedAt, ReservationId);
GO
CREATE INDEX IX_BookReservations_Member_Status ON dbo.BookReservations (MemberId, Status);
GO
CREATE INDEX IX_Loans_Status_DueDate ON dbo.Loans (Status, DueDate);
GO
CREATE INDEX IX_Loans_Member_LoanDate ON dbo.Loans (MemberId, LoanDate DESC);
GO
CREATE INDEX IX_LoanItems_BookCopyId ON dbo.LoanItems (BookCopyId);
GO
CREATE INDEX IX_FinePayments_Member_PaymentDate ON dbo.FinePayments (MemberId, PaymentDate DESC);
GO
CREATE INDEX IX_SystemLogs_CreatedAt ON dbo.SystemLogs (CreatedAt DESC);
GO

