using System.ComponentModel.DataAnnotations;

namespace csgame.Models
{
    public class BaseEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [Display(Name = "تاریخ ایجاد")]
        public DateTime CreateDateTime { get; set; } = DateTime.Now;
        [Display(Name = "حساب حذف شده؟")]
        public bool IsDelete { get; set; } = false;
    }
}
