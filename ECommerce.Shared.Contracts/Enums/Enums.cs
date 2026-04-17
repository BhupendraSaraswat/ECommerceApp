using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Shared.Contracts.Enums
{
    public enum OrderStatus
    {
        Pending = 0,
        PaymentPending = 1,
        PaymentFailed = 2,
        Confirmed = 3,
        Processing = 4,
        Shipped = 5,
        OutForDelivery = 6,
        Delivered = 7,
        Cancelled = 8,
        Refunded = 9
    }

    public enum PaymentStatus
    {
        Pending = 0,
        Processing = 1,
        Captured = 2,
        Failed = 3,
        Refunded = 4,
        PartialRefund = 5
    }

    public enum PaymentMethod
    {
        UPI = 0,
        Card = 1,
        NetBanking = 2,
        Wallet = 3,
        COD = 4,
        EMI = 5
    }

    public enum PaymentProvider
    {
        Razorpay = 0,
        Stripe = 1
    }

    public enum UserRole
    {
        Buyer = 0,
        Seller = 1,
        Admin = 2,
        SuperAdmin = 3
    }

    public enum ProductStatus
    {
        Draft = 0,
        Active = 1,
        OutOfStock = 2,
        Discontinued = 3,
        PendingApproval = 4
    }

    public enum SellerStatus
    {
        Pending = 0,
        Active = 1,
        Suspended = 2,
        Rejected = 3
    }

    public enum NotificationType
    {
        OrderPlaced = 0,
        OrderConfirmed = 1,
        OrderShipped = 2,
        OrderDelivered = 3,
        PaymentSuccess = 4,
        PaymentFailed = 5,
        SellerApproved = 6,
        ReviewPosted = 7,
        PasswordReset = 8,
        OtpVerification = 9
    }
}
