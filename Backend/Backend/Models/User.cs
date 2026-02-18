using System;
using System.Collections.Generic;

namespace Server.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; }

    public int RoleId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<FinePayment> FinePayments { get; set; } = new List<FinePayment>();

    public virtual ICollection<BookReservation> BookReservationsCreated { get; set; } = new List<BookReservation>();

    public virtual ICollection<BookReservation> BookReservationsFulfilled { get; set; } = new List<BookReservation>();

    public virtual ICollection<Notification> NotificationsCreated { get; set; } = new List<Notification>();

    public virtual ICollection<NotificationRecipient> NotificationRecipients { get; set; } = new List<NotificationRecipient>();

    public virtual ICollection<Loan> Loans { get; set; } = new List<Loan>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<SystemLog> SystemLogs { get; set; } = new List<SystemLog>();
}
