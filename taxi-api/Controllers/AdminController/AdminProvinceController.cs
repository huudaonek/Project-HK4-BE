﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;
using System.Linq;
using System.Threading.Tasks;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/admin/provinces")]
    [ApiController]
    public class AdminProvinceController : ControllerBase
    {
        private readonly TaxiContext _context;

        // Constructor to initialize context
        public AdminProvinceController(TaxiContext context)
        {
            _context = context;
        }

        [HttpGet("get-all-provinces")]
        public IActionResult GetAllProvinces([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string name = null)
        {
            // Ensure page and pageSize are valid
            if (page <= 0 || pageSize <= 0)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Page number and page size must be greater than 0."
                });
            }

            // Get the queryable list of provinces
            var query = _context.Provinces.AsQueryable();

            // Apply search filter if name is provided
            if (!string.IsNullOrEmpty(name))
            {
                query = query.Where(p => p.Name.Contains(name));
            }

            // Calculate the total number of records based on the filtered query
            var totalProvinces = query.Count();

            // Get the data for the requested page
            var provinces = query
                .Skip((page - 1) * pageSize)  // Skip the records of previous pages
                .Take(pageSize)  // Take the number of records defined by pageSize
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.DeletedAt
                })
                .ToList();

            // Calculate total pages based on total count and pageSize
            var totalPages = (int)Math.Ceiling((double)totalProvinces / pageSize);

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "The list of provinces and their prices has been successfully retrieved.",
                data = provinces,
                pagination = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalRecords = totalProvinces,
                    totalPages = totalPages
                }
            });
        }

        [HttpGet("search-location")]
        public async Task<IActionResult> GetWardInfoByName([FromQuery] string wardName)
        {
            if (string.IsNullOrEmpty(wardName))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    data = (object)null,
                    message = "Ward null ."
                });
            }

            var wardInfo = await _context.Wards
                .Where(w => EF.Functions.Like(w.Name, $"%{wardName}%"))
                .Include(w => w.District)
                .ThenInclude(d => d.Province)
                .Select(w => new
                {
                    WardId = w.Id,
                    WardName = w.Name,
                    District = new
                    {
                        DistrictId = w.District.Id,
                        DistrictName = w.District.Name,
                    },
                    Province = new
                    {
                        ProvinceId = w.District.Province.Id,
                        ProvinceName = w.District.Province.Name,
                        ProvincePrice = w.District.Province.Price
                    }
                })
                .Take(30)
                .ToListAsync();

            if (!wardInfo.Any())
            {
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    data = (object)null,
                    message = "No matching wards found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = wardInfo,
                message = "Success"
            });
        }
    }
}
