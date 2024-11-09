﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.DTO;
using taxi_api.Models;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/admin/driver")]
    [ApiController]
    public class AdminDriverController : ControllerBase
    {
        private readonly TaxiContext _context;

        public AdminDriverController(TaxiContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet("index")]
        public IActionResult Index()
        {
            var drivers = _context.Drivers
                .Join(_context.Taxies.Where(t => t.InUse == true),
                    driver => driver.Id, 
                    taxi => taxi.DriverId,
                    (driver, taxi) => new 
                    {
                        driver.Id,
                        driver.Fullname,
                        driver.Phone,
                        driver.IsActive,
                        driver.Point,
                        driver.Commission,
                        driver.CreatedAt,
                        driver.UpdatedAt,
                        driver.DeletedAt,
                        TaxiInfo = new 
                        {
                            taxi.Name,
                            taxi.LicensePlate,
                            taxi.Seat,
                            taxi.InUse,
                            taxi.CreatedAt,
                            taxi.UpdatedAt,
                            taxi.DeletedAt
                        }
                    })
                .ToList();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = drivers,
                message = "List of all drivers with their taxis retrieved successfully."
            });
        }

        [HttpPost("activate/{driverId}")]
        public IActionResult ActivateDriver(int driverId)
        {
            if (driverId <= 0)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid request. Driver ID is required."
                });
            }

            // Find the driver in the database by driverId
            var driver = _context.Drivers.FirstOrDefault(d => d.Id == driverId);
            if (driver == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    data = (object)null,
                    message = "Driver not found."
                });
            }

            // Get the default commission from the Config table
            var defaultCommissionConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "default_comission");

            if (defaultCommissionConfig == null)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    data = (object)null,
                    message = "Default commission configuration not found."
                });
            }

            // Activate the driver and set the default commission
            driver.IsActive = true;
            driver.Commission = int.Parse(defaultCommissionConfig.Value);
            _context.SaveChanges();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = new { driverId = driver.Id },
                message = "Driver account activated successfully."
            });
        }


        [HttpPost("BanDriver/{driverId}")]
        public IActionResult BanDriver(int driverId)
        {
            if (driverId <= 0)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid request. Driver ID is required."
                });
            }

            var driver = _context.Drivers.FirstOrDefault(d => d.Id == driverId);
            if (driver.DeletedAt == null)
            {
                driver.DeletedAt = DateTime.UtcNow;
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    data = (object)null,
                    message = "Driver ban sucessfully."
                });
            }
            else 
            {
                driver.DeletedAt = null;
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    data = new { driverId = driver.Id },
                    message = "Driver unban successfully."
                });
            }
        }
        [HttpPut("edit-commission/{driverId}")]
        public async Task<IActionResult> EditCommission(int driverId, [FromBody] CommissionUpdateDto commissionDto)
        {
            var driver = await _context.Drivers.FindAsync(driverId);
            if (driver == null)
            {
                return NotFound(new { code = CommonErrorCodes.NotFound, message = "Driver not found." });
            }

            driver.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { code = CommonErrorCodes.Success, message = "Commission updated successfully.", data = driver });
        }
        [HttpGet("get-all-taxis")]
        public async Task<IActionResult> GetAllTaxis()
        {
            var taxis = await _context.Taxies
                .Join(_context.Drivers, // Join với bảng Drivers
                    taxi => taxi.DriverId, // Liên kết DriverId của taxi
                    driver => driver.Id,  // Liên kết với Id của driver
                    (taxi, driver) => new // Tạo đối tượng kết quả
                    {
                        taxi.Id,
                        taxi.Name,
                        taxi.LicensePlate,
                        taxi.Seat,
                        taxi.InUse,
                        taxi.CreatedAt,
                        taxi.UpdatedAt,
                        taxi.DeletedAt,
                        Fullname = driver.Fullname // Lấy tên tài xế
                    })
                .ToListAsync(); // Dùng ToListAsync() để lấy dữ liệu bất đồng bộ

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = taxis,
                message = "List of all taxis retrieved successfully."
            });
        }
    }
}
