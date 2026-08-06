using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Server.Contracts.Loans;
using Server.Contracts.Members;
using Server.Interface;
using Server.Models;

namespace Server.DataAccess;

public sealed class LibraryDataAccess : ILibraryDataAccess
{
    private static readonly Regex PhoneRegex = new("^0\\d{9}$", RegexOptions.Compiled);
    private const decimal OverdueFinePerDayPerCopy = 2000m;
    private const decimal WornConditionFine = 10000m;
    private const decimal DamagedConditionFine = 50000m;
    private const decimal LostConditionFine = 200000m;
    private const decimal OutstandingFineLoanBlockThreshold = 50000m;
    private const int MaxBorrowedCopiesPerMember = 5;
    private const int MaxRenewalsPerLoan = 2;
    private const int MaxRenewalDays = 14;

    private static string ToVietnameseRole(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "manager" => "Quản lý",
            "staff" => "Nhân viên",
            _ => role ?? string.Empty
        };
    }

    private static string ToVietnameseLoanStatus(string? status)
    {
        return status?.Trim() switch
        {
            "Active" => "Đang mượn",
            "Borrowing" => "Đang mượn",
            "Returned" => "Đã trả",
            "Overdue" => "Quá hạn",
            "Lost" => "Mất sách",
            _ => status ?? string.Empty
        };
    }

    private static string ToVietnameseReservationStatus(string? status)
    {
        return status?.Trim() switch
        {
            "Pending" => "Đang chờ",
            "Ready" => "Đã giữ sách",
            "Fulfilled" => "Đã mượn",
            "Cancelled" => "Đã hủy",
            "Expired" => "Đã hết hạn",
            _ => status ?? string.Empty
        };
    }

    private readonly Func<LibraryContext> _dbContextFactory;

    public LibraryDataAccess(Func<LibraryContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    private static List<string> NormalizeDistinctNames(IReadOnlyList<string> names)
    {
        return names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeRoleName(string? roleName)
    {
        return (roleName ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static bool IsManagerRole(string? roleName)
    {
        return string.Equals(roleName, "manager", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEmployeeRole(string? roleName)
    {
        return string.Equals(roleName, "staff", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<User> RequireManagerAsync(LibraryContext db, string actorUsername, CancellationToken cancellationToken)
    {
        var normalizedActor = (actorUsername ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedActor))
        {
            throw new InvalidOperationException("Thiếu tài khoản thao tác.");
        }

        var actor = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == normalizedActor && u.IsActive, cancellationToken);

        if (actor is null || !IsManagerRole(actor.Role.RoleName))
        {
            throw new InvalidOperationException("Bạn không có quyền thực hiện thao tác này.");
        }

        return actor;
    }

    public async Task<(object? User, string? PasswordHash)> GetActiveUserForAuthenticationAsync(string username, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive, cancellationToken);

        if (user is null)
        {
            return (null, null);
        }

        var authUser = new
        {
            user.UserId,
            user.Username,
            user.FullName,
            Role = ToVietnameseRole(user.Role.RoleName),
            user.AvatarUrl
        };

        return (authUser, user.PasswordHash);
    }

    public async Task<object?> AuthenticateByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        return new
        {
            user.UserId,
            user.Username,
            user.FullName,
            Role = ToVietnameseRole(user.Role.RoleName),
            user.AvatarUrl
        };
    }

    public async Task<string?> GetUserRoleNameAsync(string username, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedUsername = (username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return null;
        }

        return await db.Users
            .AsNoTracking()
            .Where(u => u.Username == normalizedUsername && u.IsActive)
            .Select(u => u.Role.RoleName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int?> GetUserIdByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedUsername = (username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return null;
        }

        return await db.Users
            .AsNoTracking()
            .Where(u => u.Username == normalizedUsername && u.IsActive)
            .Select(u => (int?)u.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int?> GetUserIdByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        return await db.Users
            .AsNoTracking()
            .Where(u => u.Email == normalizedEmail && u.IsActive)
            .Select(u => (int?)u.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> GetUsernameByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        return await db.Users
            .AsNoTracking()
            .Where(u => u.Email == normalizedEmail && u.IsActive)
            .Select(u => u.Username)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<object?> GetUserProfileAsync(string username, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null)
        {
            return null;
        }

        return new
        {
            user.UserId,
            user.Username,
            user.FullName,
            user.Email,
            user.Phone,
            user.IsActive,
            user.RoleId,
            Role = ToVietnameseRole(user.Role.RoleName),
            user.CreatedAt,
            user.AvatarUrl
        };
    }

    public async Task UpdateUserProfileAsync(string username, string? newUsername, string? phone, string? avatarUrl, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("Không tìm thấy tài khoản.");
        }

        var normalizedNewUsername = string.IsNullOrWhiteSpace(newUsername) ? null : newUsername.Trim();
        if (string.IsNullOrWhiteSpace(normalizedNewUsername))
        {
            throw new InvalidOperationException("Tên đăng nhập không được để trống.");
        }

        if (!string.Equals(user.Username, normalizedNewUsername, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await db.Users.AnyAsync(u => u.Username == normalizedNewUsername, cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");
            }

            user.Username = normalizedNewUsername;
        }

        user.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserPasswordHashAsync(string username, string passwordHash, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("Không tìm thấy tài khoản.");
        }

        user.PasswordHash = passwordHash;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task ChangePasswordAsync(string username, string newPasswordHash, CancellationToken cancellationToken)
    {
        return UpdateUserPasswordHashAsync(username, newPasswordHash, cancellationToken);
    }

    public async Task<List<object>> GetUsersListAsync(string actorUsername, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        await RequireManagerAsync(db, actorUsername, cancellationToken);

        var users = await db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .OrderByDescending(u => u.UserId)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.FullName,
                u.Email,
                u.Phone,
                u.IsActive,
                u.RoleId,
                RoleName = u.Role.RoleName,
                Role = ToVietnameseRole(u.Role.RoleName),
                u.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return users.Cast<object>().ToList();
    }

    public async Task<int> CreateUserAsync(string actorUsername, string username, string fullName, string? email, string? phone, string roleName, string passwordHash, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        await RequireManagerAsync(db, actorUsername, cancellationToken);

        var normalizedUsername = (username ?? string.Empty).Trim();
        var normalizedFullName = (fullName ?? string.Empty).Trim();
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizePhone(phone);
        var normalizedRole = NormalizeRoleName(roleName);

        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            throw new InvalidOperationException("Tên đăng nhập không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(normalizedFullName))
        {
            throw new InvalidOperationException("Họ tên không được để trống.");
        }

        if (!IsEmployeeRole(normalizedRole) && !IsManagerRole(normalizedRole))
        {
            throw new InvalidOperationException("Vai trò không hợp lệ.");
        }

        ValidatePhone(normalizedPhone);
        ValidateEmail(normalizedEmail);

        if (await db.Users.AnyAsync(u => u.Username == normalizedUsername, cancellationToken))
        {
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) && await db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("Email đã tồn tại.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedPhone) && await db.Users.AnyAsync(u => u.Phone == normalizedPhone, cancellationToken))
        {
            throw new InvalidOperationException("Số điện thoại đã tồn tại.");
        }

        var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == normalizedRole, cancellationToken);
        if (role is null)
        {
            throw new InvalidOperationException("Vai trò không tồn tại.");
        }

        var user = new User
        {
            Username = normalizedUsername,
            PasswordHash = passwordHash,
            FullName = normalizedFullName,
            Email = normalizedEmail,
            Phone = normalizedPhone,
            IsActive = true,
            RoleId = role.RoleId,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.UserId;
    }

    public async Task UpdateUserAsync(string actorUsername, int userId, string? fullName, string? email, string? phone, string? roleName, bool? isActive, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        var actor = await RequireManagerAsync(db, actorUsername, cancellationToken);

        var target = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (target is null)
        {
            throw new InvalidOperationException("Không tìm thấy tài khoản.");
        }

        var normalizedFullName = (fullName ?? string.Empty).Trim();
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizePhone(phone);

        if (string.IsNullOrWhiteSpace(normalizedFullName))
        {
            throw new InvalidOperationException("Họ tên không được để trống.");
        }

        ValidatePhone(normalizedPhone);
        ValidateEmail(normalizedEmail);

        if (!string.IsNullOrWhiteSpace(normalizedEmail) && await db.Users.AnyAsync(u => u.UserId != userId && u.Email == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("Email đã tồn tại.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedPhone) && await db.Users.AnyAsync(u => u.UserId != userId && u.Phone == normalizedPhone, cancellationToken))
        {
            throw new InvalidOperationException("Số điện thoại đã tồn tại.");
        }

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            var normalizedRole = NormalizeRoleName(roleName);
            if (!IsEmployeeRole(normalizedRole) && !IsManagerRole(normalizedRole))
            {
                throw new InvalidOperationException("Vai trò không hợp lệ.");
            }

            if (target.UserId == actor.UserId && !string.Equals(target.Role.RoleName, normalizedRole, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Bạn không thể tự thay đổi vai trò của chính mình.");
            }

            var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == normalizedRole, cancellationToken);
            if (role is null)
            {
                throw new InvalidOperationException("Vai trò không tồn tại.");
            }

            target.RoleId = role.RoleId;
        }

        if (isActive.HasValue && !isActive.Value)
        {
            if (target.UserId == actor.UserId)
            {
                throw new InvalidOperationException("Bạn không thể tự vô hiệu hóa tài khoản của mình.");
            }

            if (string.Equals(target.Role.RoleName, actor.Role.RoleName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Không thể vô hiệu hóa tài khoản cùng cấp quản lý.");
            }
        }

        target.FullName = normalizedFullName;
        target.Email = normalizedEmail;
        target.Phone = normalizedPhone;
        if (isActive.HasValue)
        {
            target.IsActive = isActive.Value;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetUserPasswordAsync(string actorUsername, int userId, string newPasswordHash, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        await RequireManagerAsync(db, actorUsername, cancellationToken);

        var target = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (target is null)
        {
            throw new InvalidOperationException("Không tìm thấy tài khoản.");
        }

        if (!IsEmployeeRole(target.Role.RoleName))
        {
            throw new InvalidOperationException("Chỉ có thể đặt lại mật khẩu cho nhân viên.");
        }

        target.PasswordHash = newPasswordHash;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<object>> GetBooksListAsync(CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var books = await db.Books
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new
            {
                b.BookId,
                b.Isbn,
                b.Title,
                b.ImageUrl,
                Publisher = b.Publisher != null ? b.Publisher.PublisherName : null,
                b.PublishedYear,
                Authors = b.Authors.Select(a => a.AuthorName).ToList(),
                Categories = b.Categories.Select(c => c.CategoryName).ToList(),
                TotalCopies = b.BookCopies.Count,
                AvailableCopies = b.BookCopies.Count(c => c.CopyStatus == "Available"),
                BorrowCount = b.BookCopies.SelectMany(c => c.LoanItems).Count(),
                CanDeactivate = !b.BookCopies
                    .SelectMany(c => c.LoanItems)
                    .Any(li => li.Loan.Status == "Borrowing" || li.Loan.Status == "Overdue")
            })
            .OrderByDescending(b => b.BorrowCount)
            .ThenBy(b => b.Title)
            .ToListAsync(cancellationToken);

        return books.Cast<object>().ToList();
    }

    public async Task<int> GetMaxBarcodeAsync(CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var barcodes = await db.BookCopies
            .AsNoTracking()
            .Select(c => c.Barcode)
            .ToListAsync(cancellationToken);

        return barcodes
            .Select(b => int.TryParse(b, out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    public async Task<List<object>> GetCategoriesListAsync(CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var categories = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.CategoryName)
            .Select(c => new
            {
                c.CategoryId,
                c.CategoryName,
                c.Description,
                BookCount = c.Books.Count
            })
            .ToListAsync(cancellationToken);

        return categories.Cast<object>().ToList();
    }

    public async Task<List<object>> LookupBookCopiesAsync(string? query, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalized = (query ?? string.Empty).Trim().ToLowerInvariant();

        var copiesQuery = db.BookCopies
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new
            {
                c.BookCopyId,
                c.Barcode,
                c.CopyStatus,
                c.PhysicalCondition,
                c.LocationCode,
                c.BookId,
                c.Book.Title,
                c.Book.Isbn,
                ReservedForMemberCode = c.BookReservations
                    .Where(r => r.Status == "Ready" && r.ReservedCopyId == c.BookCopyId)
                    .Select(r => r.Member.MemberCode)
                    .FirstOrDefault(),
                ReservedForMemberName = c.BookReservations
                    .Where(r => r.Status == "Ready" && r.ReservedCopyId == c.BookCopyId)
                    .Select(r => r.Member.FullName)
                    .FirstOrDefault(),
                Authors = c.Book.Authors.Select(a => a.AuthorName).ToList()
            });

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            copiesQuery = copiesQuery.Where(c =>
                c.Barcode.ToLower().Contains(normalized) ||
                c.Isbn.ToLower().Contains(normalized) ||
                c.Title.ToLower().Contains(normalized) ||
                c.Authors.Any(a => a.ToLower().Contains(normalized)));
        }

        var result = await copiesQuery
            .OrderBy(c => c.Title)
            .ThenBy(c => c.Barcode)
            .Take(300)
            .ToListAsync(cancellationToken);

        return result.Cast<object>().ToList();
    }

    public async Task UpdateBookCopyStatusAsync(string barcode, string copyStatus, string? physicalCondition, string? locationCode, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var copy = await db.BookCopies.FirstOrDefaultAsync(c => c.Barcode == barcode, cancellationToken);
        if (copy is null)
        {
            throw new InvalidOperationException("Không tìm thấy bản sao sách.");
        }

        var isBorrowedBeforeUpdate = string.Equals(copy.CopyStatus, "Borrowed", StringComparison.OrdinalIgnoreCase);
        copy.CopyStatus = copyStatus;
        if (!isBorrowedBeforeUpdate && !string.IsNullOrWhiteSpace(physicalCondition))
        {
            copy.PhysicalCondition = physicalCondition;
        }

        if (!string.IsNullOrWhiteSpace(locationCode))
        {
            copy.LocationCode = locationCode;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CreateBookAsync(string actorUsername, string isbn, string title, string? publisherName, int? publishedYear, string? imageUrl, IReadOnlyList<string> authorNames, IReadOnlyList<string> categoryNames, int initialCopies, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        await RequireManagerAsync(db, actorUsername, cancellationToken);

        var normalizedIsbn = (isbn ?? string.Empty).Trim();
        var normalizedTitle = (title ?? string.Empty).Trim();
        var normalizedPublisherName = string.IsNullOrWhiteSpace(publisherName) ? null : publisherName.Trim();
        var normalizedImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        var normalizedAuthors = NormalizeDistinctNames(authorNames);
        var normalizedCategories = NormalizeDistinctNames(categoryNames);
        var copiesCount = initialCopies <= 0 ? 1 : initialCopies;

        if (string.IsNullOrWhiteSpace(normalizedIsbn))
        {
            throw new InvalidOperationException("ISBN không được để trống.");
        }

        if (!Regex.IsMatch(normalizedIsbn, "^\\d{13}$"))
        {
            throw new InvalidOperationException("ISBN phải gồm đúng 13 chữ số.");
        }

        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new InvalidOperationException("Tên sách không được để trống.");
        }

        if (publishedYear.HasValue)
        {
            var currentYear = DateTime.Today.Year + 1;
            if (publishedYear.Value < 1000 || publishedYear.Value > currentYear)
            {
                throw new InvalidOperationException("Năm xuất bản không hợp lệ.");
            }
        }

        if (await db.Books.AnyAsync(b => b.Isbn == normalizedIsbn, cancellationToken))
        {
            throw new InvalidOperationException("ISBN đã tồn tại.");
        }

        Publisher? publisher = null;
        if (!string.IsNullOrWhiteSpace(normalizedPublisherName))
        {
            publisher = await db.Publishers.FirstOrDefaultAsync(p => p.PublisherName == normalizedPublisherName, cancellationToken);
            if (publisher is null)
            {
                publisher = new Publisher
                {
                    PublisherName = normalizedPublisherName
                };
                db.Publishers.Add(publisher);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var book = new Book
        {
            Isbn = normalizedIsbn,
            Title = normalizedTitle,
            ImageUrl = normalizedImageUrl,
            PublisherId = publisher?.PublisherId,
            PublishedYear = publishedYear,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        if (normalizedAuthors.Count > 0)
        {
            var existingAuthors = await db.Authors
                .Where(a => normalizedAuthors.Contains(a.AuthorName))
                .ToListAsync(cancellationToken);

            foreach (var authorName in normalizedAuthors)
            {
                var author = existingAuthors.FirstOrDefault(a => string.Equals(a.AuthorName, authorName, StringComparison.OrdinalIgnoreCase));
                if (author is null)
                {
                    author = new Author { AuthorName = authorName };
                    db.Authors.Add(author);
                }

                book.Authors.Add(author);
            }
        }

        if (normalizedCategories.Count > 0)
        {
            var existingCategories = await db.Categories
                .Where(c => normalizedCategories.Contains(c.CategoryName))
                .ToListAsync(cancellationToken);

            foreach (var categoryName in normalizedCategories)
            {
                var category = existingCategories.FirstOrDefault(c => string.Equals(c.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase));
                if (category is null)
                {
                    category = new Category
                    {
                        CategoryName = categoryName,
                        Description = categoryName
                    };
                    db.Categories.Add(category);
                }

                book.Categories.Add(category);
            }
        }

        db.Books.Add(book);
        await db.SaveChangesAsync(cancellationToken);

        var existingBarcodes = await db.BookCopies
            .Select(c => c.Barcode)
            .ToListAsync(cancellationToken);
        var maxBarcodeNumber = existingBarcodes
            .Select(b => int.TryParse(b, out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max();

        if (maxBarcodeNumber + copiesCount > 99999)
        {
            throw new InvalidOperationException("Số lượng bản sao vượt quá giới hạn mã vạch 5 chữ số.");
        }

        for (var i = 1; i <= copiesCount; i++)
        {
            var barcode = (maxBarcodeNumber + i).ToString("D5");
            db.BookCopies.Add(new BookCopy
            {
                BookId = book.BookId,
                Barcode = barcode,
                AcquiredDate = DateOnly.FromDateTime(DateTime.Today),
                CopyStatus = "Available",
                PhysicalCondition = "New",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return book.BookId;
    }

    public async Task UpdateBookAsync(string actorUsername, int bookId, string isbn, string title, string? publisherName, int? publishedYear, string? imageUrl, IReadOnlyList<string> authorNames, IReadOnlyList<string> categoryNames, int desiredTotalCopies, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        await RequireManagerAsync(db, actorUsername, cancellationToken);

        var normalizedIsbn = (isbn ?? string.Empty).Trim();
        var normalizedTitle = (title ?? string.Empty).Trim();
        var normalizedPublisherName = string.IsNullOrWhiteSpace(publisherName) ? null : publisherName.Trim();
        var normalizedImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        var normalizedAuthors = NormalizeDistinctNames(authorNames);
        var normalizedCategories = NormalizeDistinctNames(categoryNames);

        if (string.IsNullOrWhiteSpace(normalizedIsbn))
        {
            throw new InvalidOperationException("ISBN không được để trống.");
        }

        if (!Regex.IsMatch(normalizedIsbn, "^\\d{13}$"))
        {
            throw new InvalidOperationException("ISBN phải gồm đúng 13 chữ số.");
        }

        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new InvalidOperationException("Tên sách không được để trống.");
        }

        if (publishedYear.HasValue)
        {
            var currentYear = DateTime.Today.Year + 1;
            if (publishedYear.Value < 1000 || publishedYear.Value > currentYear)
            {
                throw new InvalidOperationException("Năm xuất bản không hợp lệ.");
            }
        }

        if (desiredTotalCopies <= 0)
        {
            throw new InvalidOperationException("Số lượng bản sao phải lớn hơn 0.");
        }

        var book = await db.Books
            .Include(b => b.Authors)
            .Include(b => b.Categories)
            .Include(b => b.BookCopies)
            .FirstOrDefaultAsync(b => b.BookId == bookId && b.IsActive, cancellationToken);

        if (book is null)
        {
            throw new InvalidOperationException("Không tìm thấy sách đang hoạt động.");
        }

        var hasAnyBorrowHistory = await db.LoanItems.AnyAsync(li => li.BookCopy.BookId == bookId, cancellationToken);
        if (hasAnyBorrowHistory)
        {
            throw new InvalidOperationException("Chỉ được sửa sản phẩm chưa từng có ai mượn.");
        }

        var duplicatedIsbn = await db.Books
            .AnyAsync(b => b.BookId != bookId && b.Isbn == normalizedIsbn, cancellationToken);
        if (duplicatedIsbn)
        {
            throw new InvalidOperationException("ISBN đã tồn tại.");
        }

        Publisher? publisher = null;
        if (!string.IsNullOrWhiteSpace(normalizedPublisherName))
        {
            publisher = await db.Publishers.FirstOrDefaultAsync(p => p.PublisherName == normalizedPublisherName, cancellationToken);
            if (publisher is null)
            {
                publisher = new Publisher
                {
                    PublisherName = normalizedPublisherName
                };
                db.Publishers.Add(publisher);
            }
        }

        book.Isbn = normalizedIsbn;
        book.Title = normalizedTitle;
        book.Publisher = publisher;
        book.PublishedYear = publishedYear;
        if (!string.IsNullOrWhiteSpace(normalizedImageUrl))
        {
            book.ImageUrl = normalizedImageUrl;
        }

        book.Authors.Clear();
        if (normalizedAuthors.Count > 0)
        {
            var existingAuthors = await db.Authors
                .Where(a => normalizedAuthors.Contains(a.AuthorName))
                .ToListAsync(cancellationToken);

            foreach (var authorName in normalizedAuthors)
            {
                var author = existingAuthors.FirstOrDefault(a => a.AuthorName == authorName);
                if (author is null)
                {
                    author = new Author
                    {
                        AuthorName = authorName
                    };
                    db.Authors.Add(author);
                }

                book.Authors.Add(author);
            }
        }

        book.Categories.Clear();
        if (normalizedCategories.Count > 0)
        {
            var existingCategories = await db.Categories
                .Where(c => normalizedCategories.Contains(c.CategoryName))
                .ToListAsync(cancellationToken);

            foreach (var categoryName in normalizedCategories)
            {
                var category = existingCategories.FirstOrDefault(c => c.CategoryName == categoryName);
                if (category is null)
                {
                    category = new Category
                    {
                        CategoryName = categoryName,
                        Description = categoryName
                    };
                    db.Categories.Add(category);
                }

                book.Categories.Add(category);
            }
        }

        var currentTotalCopies = book.BookCopies.Count;
        if (desiredTotalCopies < currentTotalCopies)
        {
            throw new InvalidOperationException("Không thể giảm số lượng hiện có. Bạn chỉ có thể giữ nguyên hoặc tăng thêm.");
        }

        var additionalCopies = desiredTotalCopies - currentTotalCopies;
        if (additionalCopies > 0)
        {
            var existingBarcodes = await db.BookCopies
                .Select(c => c.Barcode)
                .ToListAsync(cancellationToken);
            var maxBarcodeNumber = existingBarcodes
                .Select(b => int.TryParse(b, out var parsed) ? parsed : 0)
                .DefaultIfEmpty(0)
                .Max();

            if (maxBarcodeNumber + additionalCopies > 99999)
            {
                throw new InvalidOperationException("Số lượng bản sao vượt quá giới hạn mã vạch 5 chữ số.");
            }

            for (var i = 1; i <= additionalCopies; i++)
            {
                var barcode = (maxBarcodeNumber + i).ToString("D5");
                db.BookCopies.Add(new BookCopy
                {
                    BookId = book.BookId,
                    Barcode = barcode,
                    AcquiredDate = DateOnly.FromDateTime(DateTime.Today),
                    CopyStatus = "Available",
                    PhysicalCondition = "New",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateBookAsync(string actorUsername, int bookId, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        await RequireManagerAsync(db, actorUsername, cancellationToken);

        var book = await db.Books
            .Include(b => b.BookCopies)
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken);

        if (book is null || !book.IsActive)
        {
            throw new InvalidOperationException("Không tìm thấy sách đang hoạt động.");
        }

        var hasBorrowing = await db.LoanItems
            .AnyAsync(li => li.BookCopy.BookId == bookId
                && (li.Loan.Status == "Borrowing" || li.Loan.Status == "Overdue"), cancellationToken);

        if (hasBorrowing)
        {
            throw new InvalidOperationException("Không thể ngừng bán sách đang có phiếu mượn chưa trả.");
        }

        book.IsActive = false;
        foreach (var copy in book.BookCopies)
        {
            copy.IsActive = false;
            if (string.Equals(copy.CopyStatus, "Available", StringComparison.OrdinalIgnoreCase)
                || string.Equals(copy.CopyStatus, "Maintenance", StringComparison.OrdinalIgnoreCase)
                || string.Equals(copy.CopyStatus, "Damaged", StringComparison.OrdinalIgnoreCase))
            {
                copy.CopyStatus = "Disposed";
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CreateCategoryAsync(string actorUsername, string categoryName, string? description, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        await RequireManagerAsync(db, actorUsername, cancellationToken);

        var normalizedCategoryName = (categoryName ?? string.Empty).Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (string.IsNullOrWhiteSpace(normalizedCategoryName))
        {
            throw new InvalidOperationException("Tên thể loại không được để trống.");
        }

        var exists = await db.Categories
            .AnyAsync(c => c.CategoryName.ToLower() == normalizedCategoryName.ToLower(), cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Tên thể loại đã tồn tại.");
        }

        var category = new Category
        {
            CategoryName = normalizedCategoryName,
            Description = normalizedDescription
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category.CategoryId;
    }

    public async Task<object?> GetBookDetailAsync(int bookId, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        return await db.Books
            .AsNoTracking()
            .Where(b => b.BookId == bookId && b.IsActive)
            .Select(b => new
            {
                b.BookId,
                b.Isbn,
                b.Title,
                b.ImageUrl,
                Publisher = b.Publisher != null ? b.Publisher.PublisherName : null,
                b.PublishedYear,
                Authors = b.Authors.Select(a => a.AuthorName).ToList(),
                Categories = b.Categories.Select(c => c.CategoryName).ToList(),
                TotalCopies = b.BookCopies.Count,
                AvailableCopies = b.BookCopies.Count(c => c.CopyStatus == "Available"),
                Copies = b.BookCopies
                    .OrderBy(c => c.BookCopyId)
                    .Select(c => new
                    {
                        c.BookCopyId,
                        c.Barcode,
                        c.CopyStatus,
                        c.PhysicalCondition,
                        c.LocationCode
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<object>> GetMembersListAsync(CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var today = DateOnly.FromDateTime(DateTime.Today);

        var members = await db.Members
            .AsNoTracking()
            .OrderByDescending(m => m.MemberId)
            .Select(m => new
            {
                m.MemberId,
                m.MemberCode,
                m.FullName,
                m.Email,
                m.Phone,
                m.IsActive,
                BorrowingLoans = m.Loans.Count(l => l.Status == "Borrowing" || l.Status == "Overdue"),
                OverdueLoans = m.Loans.Count(l => (l.Status == "Borrowing" || l.Status == "Overdue") && l.DueDate < today)
            })
            .ToListAsync(cancellationToken);

        return members.Cast<object>().ToList();
    }

    public async Task<int> CreateMemberAsync(string fullName, string? email, string? phone, string? addressLine, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedFullName = (fullName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedFullName))
        {
            throw new InvalidOperationException("Vui lòng nhập họ tên.");
        }

        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizePhone(phone);

        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            throw new InvalidOperationException("Số điện thoại không được để trống.");
        }

        ValidatePhone(normalizedPhone);
        ValidateEmail(normalizedEmail);

        if (!string.IsNullOrWhiteSpace(normalizedPhone) && await db.Members.AnyAsync(m => m.Phone == normalizedPhone, cancellationToken))
        {
            throw new InvalidOperationException("Số điện thoại đã tồn tại.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) && await db.Members.AnyAsync(m => m.Email == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("Email đã tồn tại.");
        }

        var lastId = await db.Members
            .OrderByDescending(m => m.MemberId)
            .Select(m => m.MemberId)
            .FirstOrDefaultAsync(cancellationToken);

        var memberCode = $"MEM{(lastId + 1):D3}";

        var member = new Member
        {
            MemberCode = memberCode,
            FullName = normalizedFullName,
            Email = normalizedEmail,
            Phone = normalizedPhone,
            AddressLine = string.IsNullOrWhiteSpace(addressLine) ? null : addressLine,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Members.Add(member);
        await db.SaveChangesAsync(cancellationToken);
        return member.MemberId;
    }

    public async Task UpdateMemberAsync(int memberId, string? email, string? phone, string? addressLine, bool? isActive, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var member = await db.Members.FirstOrDefaultAsync(m => m.MemberId == memberId, cancellationToken);
        if (member is null)
        {
            throw new InvalidOperationException("Không tìm thấy thành viên.");
        }

        var normalizedEmail = NormalizeEmail(email);
        var normalizedPhone = NormalizePhone(phone);

        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            throw new InvalidOperationException("Số điện thoại không được để trống.");
        }

        ValidatePhone(normalizedPhone);
        ValidateEmail(normalizedEmail);

        if (!string.IsNullOrWhiteSpace(normalizedPhone) && await db.Members.AnyAsync(m => m.MemberId != memberId && m.Phone == normalizedPhone, cancellationToken))
        {
            throw new InvalidOperationException("Số điện thoại đã tồn tại.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) && await db.Members.AnyAsync(m => m.MemberId != memberId && m.Email == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("Email đã tồn tại.");
        }

        member.Email = normalizedEmail;
        member.Phone = normalizedPhone;
        member.AddressLine = string.IsNullOrWhiteSpace(addressLine) ? null : addressLine;
        if (isActive.HasValue)
        {
            member.IsActive = isActive.Value;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<object?> GetMemberBorrowingStatusAsync(string memberCode, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var member = await db.Members
            .AsNoTracking()
            .Where(m => m.MemberCode == memberCode)
            .Select(m => new
            {
                m.MemberId,
                m.MemberCode,
                m.FullName,
                m.IsActive,
                BorrowingLoans = m.Loans.Count(l => l.Status == "Borrowing" || l.Status == "Overdue"),
                OverdueLoans = m.Loans.Count(l => (l.Status == "Borrowing" || l.Status == "Overdue") && l.DueDate < today),
                TotalFine = m.Loans.SelectMany(l => l.LoanItems).Sum(li => (decimal?)li.FineAmount) ?? 0m,
                TotalPaid = m.FinePayments.Sum(fp => (decimal?)fp.AmountPaid) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return null;
        }

        var outstandingFine = Math.Max(0m, member.TotalFine - member.TotalPaid);
        return new
        {
            member.MemberId,
            member.MemberCode,
            member.FullName,
            member.IsActive,
            member.BorrowingLoans,
            member.OverdueLoans,
            member.TotalFine,
            member.TotalPaid,
            OutstandingFine = outstandingFine,
            LoanBlocked = outstandingFine >= OutstandingFineLoanBlockThreshold
        };
    }

    public async Task<MemberPortalSummaryDto?> VerifyMemberAccessAsync(string memberCode, string phoneOrEmail, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedMemberCode = (memberCode ?? string.Empty).Trim();
        var normalizedCredential = (phoneOrEmail ?? string.Empty).Trim();
        var normalizedEmail = NormalizeEmail(normalizedCredential);
        var normalizedPhone = NormalizePhone(normalizedCredential);

        if (string.IsNullOrWhiteSpace(normalizedMemberCode) || string.IsNullOrWhiteSpace(normalizedCredential))
        {
            return null;
        }

        return await db.Members
            .AsNoTracking()
            .Where(m => m.MemberCode == normalizedMemberCode
                && m.IsActive
                && ((!string.IsNullOrWhiteSpace(normalizedEmail) && m.Email == normalizedEmail)
                    || (!string.IsNullOrWhiteSpace(normalizedPhone) && m.Phone == normalizedPhone)))
            .Select(m => new MemberPortalSummaryDto
            {
                MemberId = m.MemberId,
                MemberCode = m.MemberCode,
                FullName = m.FullName,
                Email = m.Email,
                Phone = m.Phone,
                IsActive = m.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MemberStatementDto?> GetMemberStatementAsync(string memberCode, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        var normalizedMemberCode = (memberCode ?? string.Empty).Trim();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var member = await db.Members
            .AsNoTracking()
            .Where(m => m.MemberCode == normalizedMemberCode && m.IsActive)
            .Select(m => new MemberPortalSummaryDto
            {
                MemberId = m.MemberId,
                MemberCode = m.MemberCode,
                FullName = m.FullName,
                Email = m.Email,
                Phone = m.Phone,
                IsActive = m.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return null;
        }

        var loans = await db.Loans
            .AsNoTracking()
            .Where(l => l.MemberId == member.MemberId)
            .OrderByDescending(l => l.LoanDate)
            .ThenByDescending(l => l.LoanId)
            .Select(l => new MemberLoanHistoryDto
            {
                LoanId = l.LoanId,
                LoanDate = l.LoanDate,
                DueDate = l.DueDate,
                ReturnDate = l.ReturnDate,
                Status = l.Status,
                StatusText = ToVietnameseLoanStatus(l.Status),
                Note = l.Note,
                RenewalCount = l.RenewalCount,
                TotalFine = l.LoanItems.Sum(li => li.FineAmount),
                Items = l.LoanItems
                    .OrderBy(li => li.LoanItemId)
                    .Select(li => new MemberLoanItemDto
                    {
                        Title = li.BookCopy.Book.Title,
                        Barcode = li.BookCopy.Barcode,
                        ConditionBefore = li.ConditionBefore,
                        ConditionAfter = li.ConditionAfter,
                        ReturnedAt = li.ReturnedAt,
                        FineAmount = li.FineAmount
                    })
                    .ToList()
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var finePayments = await db.FinePayments
            .AsNoTracking()
            .Where(fp => fp.MemberId == member.MemberId)
            .OrderByDescending(fp => fp.PaymentDate)
            .ThenByDescending(fp => fp.PaymentId)
            .Select(fp => new MemberFinePaymentDto
            {
                PaymentId = fp.PaymentId,
                PaymentDate = fp.PaymentDate,
                AmountPaid = fp.AmountPaid,
                PaymentMethod = fp.PaymentMethod,
                Note = fp.Note,
                LoanId = fp.LoanId,
                ReceivedByName = fp.ReceivedByUser != null ? fp.ReceivedByUser.FullName : null
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var reservations = await db.BookReservations
            .AsNoTracking()
            .Where(r => r.MemberId == member.MemberId)
            .OrderByDescending(r => r.RequestedAt)
            .ThenByDescending(r => r.ReservationId)
            .Select(r => new MemberReservationDto
            {
                ReservationId = r.ReservationId,
                RequestedAt = r.RequestedAt,
                BookTitle = r.Book.Title,
                Status = r.Status,
                StatusText = ToVietnameseReservationStatus(r.Status),
                ReservedBarcode = r.ReservedCopy != null ? r.ReservedCopy.Barcode : null,
                FulfilledAt = r.FulfilledAt,
                CancelledAt = r.CancelledAt,
                Note = r.Note
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var totalFine = loans.Sum(l => l.TotalFine);
        var totalPaid = finePayments.Sum(fp => fp.AmountPaid);
        var outstandingFine = Math.Max(0m, totalFine - totalPaid);

        return new MemberStatementDto
        {
            Member = member,
            BorrowingLoans = loans.Count(l => l.Status == "Borrowing" || l.Status == "Overdue"),
            OverdueLoans = loans.Count(l => (l.Status == "Borrowing" || l.Status == "Overdue") && l.DueDate < today),
            TotalFine = totalFine,
            TotalPaid = totalPaid,
            OutstandingFine = outstandingFine,
            LoanBlocked = outstandingFine >= OutstandingFineLoanBlockThreshold,
            Loans = loans,
            FinePayments = finePayments,
            Reservations = reservations
        };
    }

    public async Task<List<object>> GetLoansListAsync(CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var loans = await db.Loans
            .AsNoTracking()
            .OrderByDescending(l => l.LoanId)
            .Take(1000)
            .Select(l => new
            {
                l.LoanId,
                Member = l.Member.FullName,
                ProcessedBy = l.ProcessedByUser.FullName,
                l.LoanDate,
                l.DueDate,
                l.ReturnDate,
                l.Status,
                StatusText = ToVietnameseLoanStatus(l.Status),
                l.RenewalCount,
                ItemCount = l.LoanItems.Count,
                BookTitles = l.LoanItems
                    .Select(li => li.BookCopy.Book.Title)
                    .Where(title => !string.IsNullOrWhiteSpace(title))
                    .Distinct()
                    .OrderBy(title => title)
                    .Take(20)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return loans.Cast<object>().ToList();
    }

    public async Task<object?> GetLoanDetailAsync(int loanId, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        return await db.Loans
            .AsNoTracking()
            .Where(l => l.LoanId == loanId)
            .Select(l => new
            {
                l.LoanId,
                Member = l.Member.FullName,
                l.Status,
                l.Note,
                Items = l.LoanItems
                    .OrderBy(li => li.LoanItemId)
                    .Select(li => new
                    {
                        li.LoanItemId,
                        li.BookCopy.Barcode,
                        Title = li.BookCopy.Book.Title,
                        li.BookCopy.CopyStatus,
                        li.BookCopy.PhysicalCondition,
                        li.ReturnedAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CreateLoanAsync(string memberCode, string processedByUsername, DateOnly dueDate, IReadOnlyList<string> barcodes, string? conditionBefore, string? note, CancellationToken cancellationToken)
    {
        if (barcodes.Count == 0)
        {
            throw new InvalidOperationException("Cần ít nhất một mã vạch.");
        }

        await using var db = _dbContextFactory();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var member = await db.Members.FirstOrDefaultAsync(m => m.MemberCode == memberCode, cancellationToken);
        if (member is null || !member.IsActive)
        {
            throw new InvalidOperationException("Thành viên không tồn tại hoặc đã ngừng hoạt động.");
        }

        var hasOverdue = await db.Loans.AnyAsync(l => l.MemberId == member.MemberId && (l.Status == "Borrowing" || l.Status == "Overdue") && l.DueDate < today, cancellationToken);
        if (hasOverdue)
        {
            throw new InvalidOperationException("Thành viên đang có khoản mượn quá hạn nên không thể mượn thêm sách.");
        }

        var currentBorrowedCopies = await db.LoanItems
            .CountAsync(li => li.Loan.MemberId == member.MemberId && (li.Loan.Status == "Borrowing" || li.Loan.Status == "Overdue") && li.ReturnedAt == null, cancellationToken);

        if (currentBorrowedCopies + barcodes.Count > MaxBorrowedCopiesPerMember)
        {
            throw new InvalidOperationException($"Vượt quá giới hạn mượn {MaxBorrowedCopiesPerMember} cuốn cho mỗi thành viên.");
        }

        var totalFine = await db.LoanItems
            .Where(li => li.Loan.MemberId == member.MemberId)
            .SumAsync(li => (decimal?)li.FineAmount, cancellationToken) ?? 0m;
        var totalPaid = await db.FinePayments
            .Where(fp => fp.MemberId == member.MemberId)
            .SumAsync(fp => (decimal?)fp.AmountPaid, cancellationToken) ?? 0m;
        var outstandingFine = Math.Max(0m, totalFine - totalPaid);
        if (outstandingFine >= OutstandingFineLoanBlockThreshold)
        {
            throw new InvalidOperationException($"Thành viên đang nợ phạt {outstandingFine:N0} và chưa thể mượn thêm sách.");
        }

        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == processedByUsername && u.IsActive, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("Tài khoản xử lý phiếu mượn không hợp lệ.");
        }

        var copies = await db.BookCopies
            .Where(c => barcodes.Contains(c.Barcode))
            .ToListAsync(cancellationToken);

        if (copies.Count != barcodes.Count)
        {
            throw new InvalidOperationException("Một hoặc nhiều mã vạch không hợp lệ.");
        }

        var copyIds = copies.Select(c => c.BookCopyId).ToList();
        var readyReservationsForCopies = await db.BookReservations
            .Where(r => r.Status == "Ready"
                     && r.ReservedCopyId.HasValue
                     && copyIds.Contains(r.ReservedCopyId.Value))
            .ToListAsync(cancellationToken);

        var reservedCopyIdsForMember = readyReservationsForCopies
            .Where(r => r.MemberId == member.MemberId)
            .Select(r => r.ReservedCopyId!.Value)
            .Distinct()
            .ToHashSet();

        if (readyReservationsForCopies.Any(r => r.MemberId != member.MemberId))
        {
            throw new InvalidOperationException("Có bản sao đang được giữ cho thành viên khác theo yêu cầu đặt trước.");
        }

        if (copies.Any(c => !c.IsActive || (c.CopyStatus != "Available" && (c.CopyStatus != "Reserved" || !reservedCopyIdsForMember.Contains(c.BookCopyId)))))
        {
            throw new InvalidOperationException("Một hoặc nhiều bản sao sách hiện không sẵn sàng.");
        }

        if (copies.Any(c => string.Equals(c.PhysicalCondition, "Damaged", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(c.PhysicalCondition, "Lost", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Sách có tình trạng hư hỏng hoặc thất lạc không thể cho mượn.");
        }

        var loan = new Loan
        {
            MemberId = member.MemberId,
            ProcessedByUserId = user.UserId,
            LoanDate = today,
            DueDate = dueDate,
            Status = "Borrowing",
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };

        db.Loans.Add(loan);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var copy in copies)
        {
            db.LoanItems.Add(new LoanItem
            {
                LoanId = loan.LoanId,
                BookCopyId = copy.BookCopyId,
                ConditionBefore = conditionBefore,
                ConditionAfter = null,
                ReturnedAt = null,
                FineAmount = 0
            });

            copy.CopyStatus = "Borrowed";

            var matchedReservation = readyReservationsForCopies
                .FirstOrDefault(r => r.ReservedCopyId == copy.BookCopyId && r.MemberId == member.MemberId && r.Status == "Ready");

            if (matchedReservation is not null)
            {
                matchedReservation.Status = "Fulfilled";
                matchedReservation.FulfilledAt = DateTime.UtcNow;
                matchedReservation.FulfilledByUserId = user.UserId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return loan.LoanId;
    }

    public async Task<ReturnByBarcodeResultDto> ReturnBookByBarcodeAsync(string barcode, string? conditionAfter, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var item = await db.LoanItems
            .Include(li => li.Loan)
            .Include(li => li.BookCopy)
                .ThenInclude(c => c.Book)
            .FirstOrDefaultAsync(li => li.BookCopy.Barcode == barcode && li.ReturnedAt == null, cancellationToken);

        if (item is null)
        {
            throw new InvalidOperationException("Không tìm thấy bản ghi mượn cho mã vạch này.");
        }

        item.ReturnedAt = DateTime.UtcNow;
        item.ConditionAfter = conditionAfter;

        var overdueDays = Math.Max(0, DateOnly.FromDateTime(DateTime.Today).DayNumber - item.Loan.DueDate.DayNumber);
        var overdueFine = overdueDays * OverdueFinePerDayPerCopy;
        var conditionFine = conditionAfter switch
        {
            "Worn" => WornConditionFine,
            "Damaged" => DamagedConditionFine,
            "Lost" => LostConditionFine,
            _ => 0m
        };
        item.FineAmount = overdueFine + conditionFine;

        if (!string.IsNullOrWhiteSpace(conditionAfter))
        {
            item.BookCopy.PhysicalCondition = conditionAfter;
        }

        item.BookCopy.CopyStatus = conditionAfter switch
        {
            "Damaged" => "Damaged",
            "Lost" => "Lost",
            _ => "Available"
        };

        BookReservation? matchedReservation = null;
        if (item.BookCopy.CopyStatus == "Available")
        {
            matchedReservation = await db.BookReservations
                .Include(r => r.Member)
                .Where(r => r.BookId == item.BookCopy.BookId
                         && r.Status == "Pending"
                         && r.Member.IsActive)
                .OrderBy(r => r.RequestedAt)
                .ThenBy(r => r.ReservationId)
                .FirstOrDefaultAsync(cancellationToken);

            if (matchedReservation is not null)
            {
                matchedReservation.ReservedCopyId = item.BookCopyId;
                matchedReservation.Status = "Ready";
                item.BookCopy.CopyStatus = "Reserved";
            }
        }

        var hasOpenItems = await db.LoanItems.AnyAsync(
            li => li.LoanId == item.LoanId
               && li.LoanItemId != item.LoanItemId
               && li.ReturnedAt == null,
            cancellationToken);
        if (!hasOpenItems)
        {
            item.Loan.Status = "Returned";
            item.Loan.ReturnDate = DateOnly.FromDateTime(DateTime.Today);
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            item.Loan.Status = item.Loan.DueDate < today ? "Overdue" : "Borrowing";
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ReturnByBarcodeResultDto
        {
            LoanId = item.LoanId,
            ReservedForMemberCode = matchedReservation?.Member.MemberCode,
            ReservedForMemberName = matchedReservation?.Member.FullName,
            ReservedBookTitle = matchedReservation is null ? null : item.BookCopy.Book.Title,
            ReservedBarcode = matchedReservation is null ? null : item.BookCopy.Barcode,
            ReservationStatus = matchedReservation?.Status
        };
    }

    public async Task<List<object>> GetPendingReservationsAsync(CancellationToken cancellationToken)
    {
        var reservations = await GetReservationsAsync("open", cancellationToken);
        return reservations.Cast<object>().ToList();
    }

    public async Task<List<ReservationDto>> GetReservationsAsync(string? status, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedStatus = (status ?? "open").Trim().ToLowerInvariant();
        var query = db.BookReservations.AsNoTracking().AsQueryable();

        query = normalizedStatus switch
        {
            "all" => query,
            "pending" => query.Where(r => r.Status == "Pending"),
            "ready" => query.Where(r => r.Status == "Ready"),
            "fulfilled" => query.Where(r => r.Status == "Fulfilled"),
            "cancelled" => query.Where(r => r.Status == "Cancelled"),
            "expired" => query.Where(r => r.Status == "Expired"),
            _ => query.Where(r => r.Status == "Pending" || r.Status == "Ready")
        };

        return await query
            .OrderBy(r => r.Status == "Ready" ? 0 : 1)
            .ThenBy(r => r.RequestedAt)
            .ThenBy(r => r.ReservationId)
            .Select(r => new ReservationDto
            {
                ReservationId = r.ReservationId,
                RequestedAt = r.RequestedAt,
                Note = r.Note,
                MemberCode = r.Member.MemberCode,
                Member = r.Member.FullName,
                BookId = r.BookId,
                BookTitle = r.Book.Title,
                Status = r.Status,
                StatusText = ToVietnameseReservationStatus(r.Status),
                ReservedBarcode = r.ReservedCopy != null ? r.ReservedCopy.Barcode : null,
                CanCancel = r.Status == "Pending" || r.Status == "Ready",
                QueuePosition = r.Status == "Pending"
                    ? db.BookReservations.Count(q => q.BookId == r.BookId
                        && q.Status == "Pending"
                        && (q.RequestedAt < r.RequestedAt || (q.RequestedAt == r.RequestedAt && q.ReservationId <= r.ReservationId)))
                    : 0
            })
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateReservationAsync(string memberCode, int bookId, string actorUsername, string? note, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedMemberCode = (memberCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedMemberCode))
        {
            throw new InvalidOperationException("Thiếu mã thành viên.");
        }

        var member = await db.Members.FirstOrDefaultAsync(m => m.MemberCode == normalizedMemberCode, cancellationToken);
        if (member is null || !member.IsActive)
        {
            throw new InvalidOperationException("Thành viên không tồn tại hoặc đã ngừng hoạt động.");
        }

        var book = await db.Books.FirstOrDefaultAsync(b => b.BookId == bookId && b.IsActive, cancellationToken);
        if (book is null)
        {
            throw new InvalidOperationException("Không tìm thấy đầu sách để đặt trước.");
        }

        var actor = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == actorUsername && u.IsActive, cancellationToken);
        if (actor is null || (!IsEmployeeRole(actor.Role.RoleName) && !IsManagerRole(actor.Role.RoleName)))
        {
            throw new InvalidOperationException("Tài khoản thao tác không hợp lệ.");
        }

        var hasActiveLoanForBook = await db.LoanItems
            .AnyAsync(li => li.Loan.MemberId == member.MemberId
                         && li.BookCopy.BookId == bookId
                         && li.ReturnedAt == null
                         && (li.Loan.Status == "Borrowing" || li.Loan.Status == "Overdue"), cancellationToken);
        if (hasActiveLoanForBook)
        {
            throw new InvalidOperationException("Thành viên đang mượn đầu sách này, không cần đặt trước.");
        }

        var hasExistingOpenReservation = await db.BookReservations
            .AnyAsync(r => r.MemberId == member.MemberId
                        && r.BookId == bookId
                        && (r.Status == "Pending" || r.Status == "Ready"), cancellationToken);
        if (hasExistingOpenReservation)
        {
            throw new InvalidOperationException("Thành viên đã có yêu cầu đặt trước đang mở cho đầu sách này.");
        }

        var availableCopyExists = await db.BookCopies
            .AnyAsync(c => c.BookId == bookId
                        && c.IsActive
                        && c.CopyStatus == "Available"
                        && c.PhysicalCondition != "Lost"
                        && c.PhysicalCondition != "Damaged", cancellationToken);
        if (availableCopyExists)
        {
            throw new InvalidOperationException("Đầu sách vẫn còn bản sao có sẵn. Hãy tạo phiếu mượn trực tiếp.");
        }

        var reservation = new BookReservation
        {
            MemberId = member.MemberId,
            BookId = bookId,
            ReservedCopyId = null,
            CreatedByUserId = actor.UserId,
            RequestedAt = DateTime.UtcNow,
            Status = "Pending",
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };

        db.BookReservations.Add(reservation);
        await db.SaveChangesAsync(cancellationToken);
        return reservation.ReservationId;
    }

    public async Task<ReservationActionResultDto> CancelReservationAsync(int reservationId, string actorUsername, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedActor = (actorUsername ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedActor))
        {
            throw new InvalidOperationException("Thiếu tài khoản thao tác.");
        }

        var actor = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == normalizedActor && u.IsActive, cancellationToken);
        if (actor is null || (!IsEmployeeRole(actor.Role.RoleName) && !IsManagerRole(actor.Role.RoleName)))
        {
            throw new InvalidOperationException("Tài khoản thao tác không hợp lệ.");
        }

        var reservation = await db.BookReservations
            .Include(r => r.Member)
            .Include(r => r.ReservedCopy)
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);
        if (reservation is null)
        {
            throw new InvalidOperationException("Không tìm thấy yêu cầu đặt trước.");
        }

        if (reservation.Status != "Pending" && reservation.Status != "Ready")
        {
            throw new InvalidOperationException("Chỉ có thể hủy yêu cầu đặt trước đang chờ hoặc đã giữ sách.");
        }

        var releasedCopy = reservation.ReservedCopy;
        var releasedBarcode = releasedCopy?.Barcode;
        var shouldReleaseCopy = reservation.Status == "Ready"
            && releasedCopy is not null
            && string.Equals(releasedCopy.CopyStatus, "Reserved", StringComparison.OrdinalIgnoreCase);

        reservation.Status = "Cancelled";
        reservation.CancelledAt = DateTime.UtcNow;

        ReservationActionResultDto result;
        if (shouldReleaseCopy)
        {
            var reassigned = await AssignCopyToNextPendingReservationAsync(db, releasedCopy!, reservation.BookId, reservation.ReservationId, cancellationToken);
            result = new ReservationActionResultDto
            {
                ReservationId = reservation.ReservationId,
                Status = reservation.Status,
                StatusText = ToVietnameseReservationStatus(reservation.Status),
                ReleasedBarcode = releasedBarcode,
                ReassignedReservationId = reassigned?.ReservationId,
                ReassignedMemberCode = reassigned?.Member.MemberCode,
                ReassignedBarcode = reassigned is null ? null : releasedBarcode
            };
        }
        else
        {
            result = new ReservationActionResultDto
            {
                ReservationId = reservation.ReservationId,
                Status = reservation.Status,
                StatusText = ToVietnameseReservationStatus(reservation.Status),
                ReleasedBarcode = releasedBarcode
            };
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static async Task<BookReservation?> AssignCopyToNextPendingReservationAsync(LibraryContext db, BookCopy copy, int bookId, int excludedReservationId, CancellationToken cancellationToken)
    {
        var nextReservation = await db.BookReservations
            .Include(r => r.Member)
            .Where(r => r.BookId == bookId
                     && r.ReservationId != excludedReservationId
                     && r.Status == "Pending"
                     && r.Member.IsActive)
            .OrderBy(r => r.RequestedAt)
            .ThenBy(r => r.ReservationId)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextReservation is null)
        {
            copy.CopyStatus = "Available";
            return null;
        }

        nextReservation.ReservedCopyId = copy.BookCopyId;
        nextReservation.Status = "Ready";
        copy.CopyStatus = "Reserved";
        return nextReservation;
    }

    private static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    private static string? NormalizePhone(string? phone)
    {
        return string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }

    private static void ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return;
        }

        if (!PhoneRegex.IsMatch(phone))
        {
            throw new InvalidOperationException("Số điện thoại phải bắt đầu bằng số 0 và gồm đúng 10 chữ số.");
        }
    }

    private static void ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        try
        {
            _ = new MailAddress(email);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Email không đúng định dạng.");
        }
    }

    public async Task RenewLoanAsync(int loanId, DateOnly newDueDate, CancellationToken cancellationToken)
    {
        await RenewLoanWithResultAsync(loanId, newDueDate, cancellationToken);
    }

    public async Task<RenewLoanResultDto> RenewLoanWithResultAsync(int loanId, DateOnly newDueDate, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var loan = await db.Loans
            .Include(l => l.LoanItems)
                .ThenInclude(li => li.BookCopy)
            .FirstOrDefaultAsync(l => l.LoanId == loanId, cancellationToken);

        if (loan is null)
        {
            throw new InvalidOperationException("Không tìm thấy phiếu mượn.");
        }

        if (loan.Status is "Returned" or "Lost" or "Overdue")
        {
            throw new InvalidOperationException("Phiếu mượn đã trả, quá hạn hoặc mất sách không thể gia hạn.");
        }

        if (loan.Status != "Borrowing")
        {
            throw new InvalidOperationException("Chỉ phiếu đang mượn mới có thể gia hạn.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (loan.DueDate < today)
        {
            throw new InvalidOperationException("Phiếu mượn đã quá hạn nên không thể gia hạn.");
        }

        if (newDueDate <= today)
        {
            throw new InvalidOperationException("Hạn trả mới phải sau ngày hiện tại.");
        }

        if (newDueDate <= loan.DueDate)
        {
            throw new InvalidOperationException("Hạn trả mới phải sau hạn trả hiện tại của phiếu mượn.");
        }

        if (newDueDate.DayNumber - loan.DueDate.DayNumber > MaxRenewalDays)
        {
            throw new InvalidOperationException($"Mỗi lần gia hạn chỉ được tối đa {MaxRenewalDays} ngày.");
        }

        if (loan.RenewalCount >= MaxRenewalsPerLoan)
        {
            throw new InvalidOperationException($"Phiếu mượn đã đạt giới hạn {MaxRenewalsPerLoan} lần gia hạn.");
        }

        var hasActiveItems = loan.LoanItems.Any(li => li.ReturnedAt == null);
        if (!hasActiveItems)
        {
            throw new InvalidOperationException("Phiếu mượn không còn sách đang mượn để gia hạn.");
        }

        var totalFine = await db.LoanItems
            .Where(li => li.Loan.MemberId == loan.MemberId)
            .SumAsync(li => (decimal?)li.FineAmount, cancellationToken) ?? 0m;
        var totalPaid = await db.FinePayments
            .Where(fp => fp.MemberId == loan.MemberId)
            .SumAsync(fp => (decimal?)fp.AmountPaid, cancellationToken) ?? 0m;
        var outstandingFine = Math.Max(0m, totalFine - totalPaid);
        if (outstandingFine >= OutstandingFineLoanBlockThreshold)
        {
            throw new InvalidOperationException($"Thành viên đang nợ phạt {outstandingFine:N0} và chưa thể gia hạn phiếu mượn.");
        }

        var activeBookIds = loan.LoanItems
            .Where(li => li.ReturnedAt == null)
            .Select(li => li.BookCopy.BookId)
            .Distinct()
            .ToList();

        var hasOpenReservationForAnotherMember = await db.BookReservations
            .AnyAsync(r => activeBookIds.Contains(r.BookId)
                        && r.MemberId != loan.MemberId
                        && (r.Status == "Pending" || r.Status == "Ready"), cancellationToken);
        if (hasOpenReservationForAnotherMember)
        {
            throw new InvalidOperationException("Không thể gia hạn vì có thành viên khác đang đặt trước một hoặc nhiều đầu sách trong phiếu.");
        }

        var oldDueDate = loan.DueDate;
        loan.DueDate = newDueDate;
        loan.RenewalCount += 1;
        loan.Status = "Borrowing";
        await db.SaveChangesAsync(cancellationToken);

        return new RenewLoanResultDto
        {
            LoanId = loan.LoanId,
            OldDueDate = oldDueDate,
            NewDueDate = loan.DueDate,
            RenewalCount = loan.RenewalCount,
            Status = loan.Status
        };
    }

    public async Task<object> GetManagerDashboardAsync(CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var memberFineTotals = await db.LoanItems
            .AsNoTracking()
            .Where(li => li.FineAmount > 0)
            .GroupBy(li => li.Loan.MemberId)
            .Select(g => new { MemberId = g.Key, TotalFine = g.Sum(x => x.FineAmount) })
            .ToListAsync(cancellationToken);

        var memberPaymentTotals = await db.FinePayments
            .AsNoTracking()
            .GroupBy(fp => fp.MemberId)
            .Select(g => new { MemberId = g.Key, TotalPaid = g.Sum(x => x.AmountPaid) })
            .ToListAsync(cancellationToken);

        var paidByMember = memberPaymentTotals.ToDictionary(x => x.MemberId, x => x.TotalPaid);
        decimal outstandingFine = 0m;
        foreach (var fine in memberFineTotals)
        {
            var paid = paidByMember.TryGetValue(fine.MemberId, out var value) ? value : 0m;
            outstandingFine += Math.Max(0m, fine.TotalFine - paid);
        }

        var lowStockAlerts = await db.Books
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new
            {
                b.Isbn,
                b.Title,
                AvailableCopies = b.BookCopies.Count(c => c.IsActive && c.CopyStatus == "Available"),
                TotalCopies = b.BookCopies.Count(c => c.IsActive)
            })
            .Where(x => x.TotalCopies > 0 && x.AvailableCopies <= 1)
            .OrderBy(x => x.AvailableCopies)
            .ThenBy(x => x.Title)
            .Take(6)
            .ToListAsync(cancellationToken);

        var qualityAlerts = await db.Books
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new
            {
                b.Isbn,
                b.Title,
                DamagedCopies = b.BookCopies.Count(c => c.CopyStatus == "Damaged"),
                LostCopies = b.BookCopies.Count(c => c.CopyStatus == "Lost")
            })
            .Where(x => x.DamagedCopies > 0 || x.LostCopies > 0)
            .OrderByDescending(x => x.LostCopies)
            .ThenByDescending(x => x.DamagedCopies)
            .ThenBy(x => x.Title)
            .Take(6)
            .ToListAsync(cancellationToken);

        var inventoryAlerts = lowStockAlerts
            .Select(x => new
            {
                AlertType = "LowStock",
                ItemCode = x.Isbn,
                ItemName = x.Title,
                Detail = $"Còn {x.AvailableCopies}/{x.TotalCopies} bản sẵn sàng"
            })
            .Concat(qualityAlerts.Select(x => new
            {
                AlertType = "Quality",
                ItemCode = x.Isbn,
                ItemName = x.Title,
                Detail = $"Mất: {x.LostCopies}, Hư hỏng: {x.DamagedCopies}"
            }))
            .Take(10)
            .ToList();

        var revenueYear = DateTime.Today.Year;
        var yearStart = new DateTime(revenueYear, 1, 1);
        var nextYearStart = yearStart.AddYears(1);
        var lastMonthStart = monthStart.AddMonths(-1);

        var monthlyRevenueRows = await db.FinePayments
            .AsNoTracking()
            .Where(fp => fp.PaymentDate >= yearStart && fp.PaymentDate < nextYearStart)
            .GroupBy(fp => fp.PaymentDate.Month)
            .Select(g => new
            {
                Month = g.Key,
                TotalFineCollected = g.Sum(x => x.AmountPaid),
                PaymentCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        var monthlyRevenues = Enumerable.Range(1, 12)
            .Select(month =>
            {
                var found = monthlyRevenueRows.FirstOrDefault(x => x.Month == month);
                return new
                {
                    Month = month,
                    TotalFineCollected = found?.TotalFineCollected ?? 0m,
                    PaymentCount = found?.PaymentCount ?? 0
                };
            })
            .ToList();

        var collectedFineThisMonth = monthlyRevenues.First(x => x.Month == DateTime.Today.Month).TotalFineCollected;
        var revenueLastMonth = lastMonthStart.Year == revenueYear
            ? monthlyRevenues.First(x => x.Month == lastMonthStart.Month).TotalFineCollected
            : 0m;

        return new
        {
            TotalBookTitles = await db.Books.CountAsync(cancellationToken),
            TotalBookCopies = await db.BookCopies.CountAsync(cancellationToken),
            BorrowedCopies = await db.BookCopies.CountAsync(c => c.CopyStatus == "Borrowed", cancellationToken),
            ActiveMembers = await db.Members.CountAsync(m => m.IsActive, cancellationToken),
            TodayLoans = await db.Loans.CountAsync(l => l.LoanDate == today, cancellationToken),
            OverdueLoans = await db.Loans.CountAsync(l => (l.Status == "Borrowing" || l.Status == "Overdue") && l.DueDate < today, cancellationToken),
            LostOrDamagedCopies = await db.BookCopies.CountAsync(c => c.CopyStatus == "Lost" || c.CopyStatus == "Damaged", cancellationToken),
            OutstandingFine = outstandingFine,
            CollectedFineThisMonth = collectedFineThisMonth,
            RevenueYear = revenueYear,
            RevenueThisYear = monthlyRevenues.Sum(x => x.TotalFineCollected),
            RevenueLastMonth = revenueLastMonth,
            MonthlyRevenues = monthlyRevenues,
            InventoryAlerts = inventoryAlerts
        };
    }

    public async Task<object> GetManagerRevenueAsync(int year, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var monthly = await db.FinePayments
            .AsNoTracking()
            .Where(fp => fp.PaymentDate.Year == year)
            .GroupBy(fp => fp.PaymentDate.Month)
            .Select(g => new
            {
                Month = g.Key,
                TotalFineCollected = g.Sum(x => x.AmountPaid),
                PaymentCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        var items = Enumerable.Range(1, 12)
            .Select(month =>
            {
                var found = monthly.FirstOrDefault(x => x.Month == month);
                return (object)new
                {
                    Month = month,
                    TotalFineCollected = found?.TotalFineCollected ?? 0m,
                    PaymentCount = found?.PaymentCount ?? 0
                };
            })
            .ToList();

        return new
        {
            Year = year,
            Items = items
        };
    }

    public async Task<int> CollectFinePaymentAsync(string? memberCode, int? loanId, decimal amountPaid, string? paymentMethod, string? note, string receivedByUsername, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        if (amountPaid <= 0)
        {
            throw new InvalidOperationException("Số tiền thu phải lớn hơn 0.");
        }

        var staffUsername = (receivedByUsername ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(staffUsername))
        {
            throw new InvalidOperationException("Thiếu tài khoản nhân sự thu tiền phạt.");
        }

        var receiver = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == staffUsername && u.IsActive, cancellationToken);
        if (receiver is null)
        {
            throw new InvalidOperationException("Không tìm thấy nhân sự thu tiền phạt.");
        }

        if (!IsEmployeeRole(receiver.Role.RoleName))
        {
            throw new InvalidOperationException("Chỉ nhân viên mới được phép thu tiền phạt.");
        }

        Loan? loan = null;
        if (loanId.HasValue)
        {
            loan = await db.Loans.FirstOrDefaultAsync(l => l.LoanId == loanId.Value, cancellationToken);
            if (loan is null)
            {
                throw new InvalidOperationException("Không tìm thấy phiếu mượn.");
            }
        }

        Member? member = null;
        var normalizedMemberCode = (memberCode ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(normalizedMemberCode))
        {
            member = await db.Members.FirstOrDefaultAsync(m => m.MemberCode == normalizedMemberCode, cancellationToken);
            if (member is null)
            {
                throw new InvalidOperationException("Không tìm thấy thành viên.");
            }
        }

        if (loan is null && member is null)
        {
            throw new InvalidOperationException("Cần cung cấp mã thành viên hoặc mã phiếu mượn để thu phạt.");
        }

        if (loan is not null && member is not null && loan.MemberId != member.MemberId)
        {
            throw new InvalidOperationException("Thành viên và phiếu mượn không khớp nhau.");
        }

        var targetMemberId = member?.MemberId ?? loan!.MemberId;

        var totalFine = await db.LoanItems
            .Where(li => li.Loan.MemberId == targetMemberId)
            .SumAsync(li => (decimal?)li.FineAmount, cancellationToken) ?? 0m;

        var totalPaid = await db.FinePayments
            .Where(fp => fp.MemberId == targetMemberId)
            .SumAsync(fp => (decimal?)fp.AmountPaid, cancellationToken) ?? 0m;

        var outstanding = Math.Max(0m, totalFine - totalPaid);
        if (outstanding <= 0m)
        {
            throw new InvalidOperationException("Thành viên hiện không còn nợ phạt.");
        }

        if (amountPaid > outstanding)
        {
            throw new InvalidOperationException($"Số tiền thu vượt quá công nợ hiện tại ({outstanding:N0}).");
        }

        var payment = new FinePayment
        {
            MemberId = targetMemberId,
            LoanId = loan?.LoanId,
            AmountPaid = amountPaid,
            PaymentDate = DateTime.Now,
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "cash" : paymentMethod.Trim().ToLowerInvariant(),
            ReceivedByUserId = receiver.UserId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };

        db.FinePayments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        return payment.PaymentId;
    }

    public async Task<object> GetFinePaymentHistoryAsync(string? memberKeyword, string? receivedByKeyword, string? exactReceivedByUsername, DateOnly? fromDate, DateOnly? toDate, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedMemberKeyword = (memberKeyword ?? string.Empty).Trim();
        var normalizedReceiverKeyword = (receivedByKeyword ?? string.Empty).Trim();
        var normalizedExactReceiver = (exactReceivedByUsername ?? string.Empty).Trim();
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 200);

        var query = db.FinePayments
            .AsNoTracking()
            .Include(fp => fp.Member)
            .Include(fp => fp.ReceivedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedMemberKeyword))
        {
            query = query.Where(fp =>
                fp.Member.MemberCode.Contains(normalizedMemberKeyword)
                || fp.Member.FullName.Contains(normalizedMemberKeyword));
        }

        if (!string.IsNullOrWhiteSpace(normalizedExactReceiver))
        {
            query = query.Where(fp => fp.ReceivedByUser != null
                && fp.ReceivedByUser.Username == normalizedExactReceiver);
        }
        else if (!string.IsNullOrWhiteSpace(normalizedReceiverKeyword))
        {
            query = query.Where(fp => fp.ReceivedByUser != null
                && (fp.ReceivedByUser.Username.Contains(normalizedReceiverKeyword)
                    || fp.ReceivedByUser.FullName.Contains(normalizedReceiverKeyword)));
        }

        if (fromDate.HasValue)
        {
            var fromDateTime = fromDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(fp => fp.PaymentDate >= fromDateTime);
        }

        if (toDate.HasValue)
        {
            var toExclusive = toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(fp => fp.PaymentDate < toExclusive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)safePageSize);
        if (safePage > totalPages)
        {
            safePage = totalPages;
        }

        var history = await query
            .OrderByDescending(fp => fp.PaymentDate)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(fp => new
            {
                fp.PaymentId,
                fp.PaymentDate,
                fp.AmountPaid,
                fp.PaymentMethod,
                fp.Note,
                fp.LoanId,
                fp.MemberId,
                MemberCode = fp.Member.MemberCode,
                MemberName = fp.Member.FullName,
                ReceivedByUsername = fp.ReceivedByUser != null ? fp.ReceivedByUser.Username : null,
                ReceivedByName = fp.ReceivedByUser != null ? fp.ReceivedByUser.FullName : null
            })
            .ToListAsync(cancellationToken);

        return new
        {
            Items = history,
            Pagination = new
            {
                Page = safePage,
                PageSize = safePageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            }
        };
    }

    public async Task<int> CreateNotificationAsync(string actorUsername, string title, string content, bool sendToAll, IReadOnlyList<string> recipientUsernames, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        var actor = await RequireManagerAsync(db, actorUsername, cancellationToken);

        var normalizedTitle = (title ?? string.Empty).Trim();
        var normalizedContent = (content ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedTitle) || string.IsNullOrWhiteSpace(normalizedContent))
        {
            throw new InvalidOperationException("Vui lòng nhập tiêu đề và nội dung thông báo.");
        }

        var normalizedRecipients = recipientUsernames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var recipients = sendToAll
            ? await db.Users
                .Where(u => u.IsActive)
                .Select(u => new { u.UserId })
                .ToListAsync(cancellationToken)
            : await db.Users
                .Where(u => u.IsActive && normalizedRecipients.Contains(u.Username))
                .Select(u => new { u.UserId })
                .ToListAsync(cancellationToken);

        if (recipients.Count == 0)
        {
            throw new InvalidOperationException("Không có người nhận hợp lệ cho thông báo.");
        }

        var notification = new Notification
        {
            Title = normalizedTitle,
            Content = normalizedContent,
            CreatedByUserId = actor.UserId,
            SendToAll = sendToAll,
            CreatedAt = DateTime.Now
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            db.NotificationRecipients.Add(new NotificationRecipient
            {
                NotificationId = notification.NotificationId,
                RecipientUserId = recipient.UserId,
                IsRead = false
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return notification.NotificationId;
    }

    public async Task<List<object>> GetNotificationsForUserAsync(string actorUsername, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedActor = (actorUsername ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedActor))
        {
            return new List<object>();
        }

        var userId = await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.Username == normalizedActor)
            .Select(u => (int?)u.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!userId.HasValue)
        {
            return new List<object>();
        }

        var items = await db.NotificationRecipients
            .AsNoTracking()
            .Where(r => r.RecipientUserId == userId.Value)
            .OrderByDescending(r => r.Notification.CreatedAt)
            .Select(r => new
            {
                r.NotificationId,
                r.Notification.Title,
                Preview = r.Notification.Content.Length > 120
                    ? r.Notification.Content.Substring(0, 120) + "..."
                    : r.Notification.Content,
                r.Notification.CreatedAt,
                CreatedByUserName = r.Notification.CreatedByUser.Username,
                CreatedByFullName = r.Notification.CreatedByUser.FullName,
                r.Notification.SendToAll,
                r.IsRead
            })
            .ToListAsync(cancellationToken);

        return items.Cast<object>().ToList();
    }

    public async Task<object?> GetNotificationDetailForUserAsync(string actorUsername, int notificationId, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var normalizedActor = (actorUsername ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedActor))
        {
            return null;
        }

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IsActive && u.Username == normalizedActor, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var recipient = await db.NotificationRecipients
            .Include(r => r.Notification)
                .ThenInclude(n => n.CreatedByUser)
            .FirstOrDefaultAsync(r => r.NotificationId == notificationId && r.RecipientUserId == user.UserId, cancellationToken);

        if (recipient is null)
        {
            return null;
        }

        if (!recipient.IsRead)
        {
            recipient.IsRead = true;
            recipient.ReadAt = DateTime.Now;
            await db.SaveChangesAsync(cancellationToken);
        }

        List<string> recipients;
        if (recipient.Notification.SendToAll)
        {
            recipients = new List<string> { "Tất cả nhân sự" };
        }
        else
        {
            recipients = await db.NotificationRecipients
                .AsNoTracking()
                .Where(x => x.NotificationId == notificationId)
                .OrderBy(x => x.RecipientUser.Username)
                .Select(x => x.RecipientUser.Username)
                .ToListAsync(cancellationToken);
        }

        return new
        {
            recipient.NotificationId,
            recipient.Notification.Title,
            recipient.Notification.Content,
            recipient.Notification.CreatedAt,
            CreatedByUserName = recipient.Notification.CreatedByUser.Username,
            CreatedByFullName = recipient.Notification.CreatedByUser.FullName,
            recipient.Notification.SendToAll,
            Recipients = recipients,
            recipient.IsRead,
            recipient.ReadAt
        };
    }

    public async Task<List<object>> GetSystemLogsAsync(CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var logs = await db.SystemLogs
            .AsNoTracking()
            .OrderByDescending(l => l.LogId)
            .Take(500)
            .Select(l => new
            {
                l.LogId,
                l.ActionType,
                l.EntityName,
                l.EntityId,
                l.Description,
                l.IpAddress,
                l.CreatedAt,
                UserName = l.User != null ? l.User.Username : null
            })
            .ToListAsync(cancellationToken);

        return logs.Cast<object>().ToList();
    }

    public async Task AddSystemLogAsync(string actionType, string entityName, string? entityId, string? description, int? userId, CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();
        db.SystemLogs.Add(new SystemLog
        {
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            Description = description,
            UserId = userId,
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<object>> GetOverdueReportAsync(CancellationToken cancellationToken)
    {
        await using var db = _dbContextFactory();

        var today = DateOnly.FromDateTime(DateTime.Today);

        var overdueLoans = await db.Loans
            .AsNoTracking()
            .Where(l => (l.Status == "Borrowing" || l.Status == "Overdue") && l.DueDate < today)
            .OrderBy(l => l.DueDate)
            .Select(l => new
            {
                l.LoanId,
                Member = l.Member.FullName,
                l.LoanDate,
                l.DueDate,
                l.Status,
                ItemCount = l.LoanItems.Count
            })
            .ToListAsync(cancellationToken);

        return overdueLoans
            .Select(l => (object)new
            {
                l.LoanId,
                l.Member,
                l.LoanDate,
                l.DueDate,
                DaysOverdue = (DateTime.Today - l.DueDate.ToDateTime(TimeOnly.MinValue)).Days,
                l.Status,
                l.ItemCount
            })
            .ToList();
    }
}
