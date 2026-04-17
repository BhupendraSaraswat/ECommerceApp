using ECommerce.Shared.Contracts.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Shared.Contracts.Events
{
    // ✅ Base — sab events isko inherit karte hain
    public abstract class BaseEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    }

    // ✅ Order place hone pe publish hoga
    public class OrderPlacedEvent : BaseEvent
    {
        public string OrderId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "INR";
        public List<OrderItemEvent> Items { get; set; } = new List<OrderItemEvent>();
        public AddressEvent ShippingAddress { get; set; } = new AddressEvent();
    }

    public class OrderItemEvent
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class AddressEvent
    {
        public string FullName { get; set; } = string.Empty;
        public string Line1 { get; set; } = string.Empty;
        public string Line2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    // ✅ Payment complete hone pe publish hoga
    public class PaymentCompletedEvent : BaseEvent
    {
        public string OrderId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string RazorpayPaymentId { get; set; } = string.Empty;
        public PaymentStatus Status { get; set; }
        public PaymentMethod Method { get; set; }
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
    }

    // ✅ Refund hone pe publish hoga
    public class PaymentRefundedEvent : BaseEvent
    {
        public string OrderId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string RefundId { get; set; } = string.Empty;
    }

    // ✅ User register hone pe publish hoga
    public class UserRegisteredEvent : BaseEvent
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public UserRole Role { get; set; }
    }

    // ✅ Seller approve hone pe publish hoga
    public class SellerApprovedEvent : BaseEvent
    {
        public string SellerId { get; set; } = string.Empty;
        public string SellerEmail { get; set; } = string.Empty;
        public string SellerName { get; set; } = string.Empty;
    }
}
