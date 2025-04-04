using System.ComponentModel.DataAnnotations;

namespace csgame.Models
{
    public class User : BaseEntity
    {
        [Display(Name = "رمز عبور")]
        [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
        [MaxLength(150, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد")]
        public string Password { get; set; }
        [Display(Name = "ایمیل")]
        [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
        [MaxLength(50, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد")]
        [EmailAddress(ErrorMessage = "فرمت {0} وارد شده صحیح نیست")]
        public string Email { get; set; }
        [Display(Name = "صطح دسترسی")]
        public string Role { get; set; } = "User";
        [Display(Name = "وضعیت حساب")]
        public bool IsActive { get; set; } = true;
        [Display(Name = "شماره تماس")]
        [RegularExpression(@"^(\d{11})$", ErrorMessage = "شماره تماس باید 11 رقم باشد")]
        public string? PhoneNumber { get; set; }
        [Display(Name = "مدیر سایت؟")]
        public bool IsAdmin { get; set; } = false;


        // Navigation properties
        public virtual ICollection<Invoice> Invoices { get; set; }
        public virtual ICollection<Subscription> Subscriptions { get; set; }

        public User()
        {
            Invoices = new HashSet<Invoice>();
            Subscriptions = new HashSet<Subscription>();
        }
    }
}
