using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BTLWebVanChuyen.Utility
{
    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum enumValue)
        {
            // Lấy FieldInfo của thành viên enum hiện tại
            var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());

            if (fieldInfo == null)
                return enumValue.ToString(); // Trả về tên mặc định nếu không tìm thấy

            // Kiểm tra xem có thuộc tính DisplayAttribute không
            var displayAttribute = fieldInfo.GetCustomAttribute<DisplayAttribute>();

            // Trả về DisplayName nếu có, nếu không trả về tên mặc định
            return displayAttribute?.Name ?? enumValue.ToString();
        }
    }
}
