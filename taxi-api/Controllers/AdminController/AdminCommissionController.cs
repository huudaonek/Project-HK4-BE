using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using taxi_api.Models;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminCommissionController : ControllerBase
    {
        private readonly TaxiContext _context;

        public AdminCommissionController(TaxiContext context)
        {
            _context = context;
        }
        [HttpPut("edit-commission/{driverId}")]
        public IActionResult EditCommission(int driverId, [FromBody] int newCommission)
        {
            if (newCommission < 0 || newCommission > 100) 
            {
                return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "Commission phải nằm trong khoảng từ 0 đến 100." });
            }

            // Tìm tài xế theo Id
            var driver = _context.Drivers.FirstOrDefault(d => d.Id == driverId);
            if (driver == null)
            {
                return NotFound(new { code = CommonErrorCodes.NotFound, message = "Không tìm thấy tài xế." });
            }

            driver.Commission = newCommission;
            driver.UpdatedAt = DateTime.Now;

            try
            {
                _context.SaveChanges();
                return Ok(new { code = CommonErrorCodes.Success, message = "Đã cập nhật Commission cho tài xế thành công.", data = new { driverId = driver.Id, newCommission = driver.Commission } });
            }
            catch (Exception)
            {
                return StatusCode(500, new { code = CommonErrorCodes.ServerError, message = "Đã xảy ra lỗi trong quá trình lưu dữ liệu." });
            }
        }
    }
}
