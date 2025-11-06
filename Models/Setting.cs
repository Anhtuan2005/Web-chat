using System.ComponentModel.DataAnnotations;

namespace Online_chat.Models
{
    public class Setting
    {
        public int Id { get; set; }

        [Display(Name = "Tên trang web")]
        public string SiteName { get; set; }

        [Display(Name = "Cho phép người dùng mới đăng ký")]
        public bool AllowNewRegistrations { get; set; }
    }
}