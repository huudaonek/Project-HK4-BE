﻿using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;
using taxi_api.DTO;
using taxi_api.Helpers;
using Newtonsoft.Json;
using Twilio.Types;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace taxi_api.Controllers.UserController
{
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly TaxiContext _context;
        private readonly IConfiguration configuation;


        public UserController(TaxiContext context, IConfiguration configuation)
        {
            _context = context;
            this.configuation = configuation;
        }
        // GET api/user/search-booking?code=XG123456
        [HttpGet("search-booking")]
        public async Task<IActionResult> SearchBooking(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Please enter the trip code."
                });
            }

            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Arival)
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.Code == code);

            if (booking == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    data = (object)null,
                    message = "No trip found with the entered code."
                });
            }

            string maskedPhone = MaskPhoneNumber(booking.Customer.Phone);

            var pickUpWard = await _context.Wards
                .Where(w => w.Id == booking.Arival.PickUpId)
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
                .FirstOrDefaultAsync();

            var dropOffWard = await _context.Wards
                .Where(w => w.Id == booking.Arival.DropOffId)
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
                .FirstOrDefaultAsync();

            var taxies = await _context.Taxies.ToListAsync();

            var driverAssignments = booking.BookingDetails
                .Select(bd => new
                {
                    bd.BookingId,
                    bd.Status,
                    bd.TaxiId,
                    TaxiDetails = taxies.FirstOrDefault(t => t.Id == bd.TaxiId)
                })
                .ToList();

            var response = new
            {
                code = CommonErrorCodes.Success,
                data = new
                {
                    BookingId = booking.Id,
                    booking.Code,
                    booking.StartAt,
                    booking.EndAt,
                    booking.Count,
                    booking.Status,
                    Customer = new
                    {
                        booking.Customer.Name,
                        Phone = maskedPhone
                    },
                    ArivalDetails = new
                    {
                        booking.Arival.PickUpAddress,
                        booking.Arival.DropOffAddress,
                        booking.Arival.Price,
                        PickUpId = booking.Arival.PickUpId,
                        PickUpDetails = pickUpWard,
                        DropOffId = booking.Arival.DropOffId,
                        DropOffDetails = dropOffWard
                    },
                    DriverAssignments = driverAssignments
                },
                message = "Success"
            };

            if (!driverAssignments.Any(da => da.TaxiDetails != null))
            {
                response = new
                {
                    code = CommonErrorCodes.Success,
                    data = response.data,
                    message = "This trip does not have a driver."
                };
            }

            return Ok(response);
        }
        private string MaskPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 7)
                return phoneNumber; 

            return phoneNumber.Substring(0, 4) + "xxx" + phoneNumber.Substring(phoneNumber.Length - 3);
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

        [HttpPost("store")]
        public async Task<IActionResult> Store([FromBody] BookingRequestDto request)
        {
            // Validate the request
            if (request == null)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid data."
                });
            }

            Customer customer;

            if (!string.IsNullOrEmpty(request.Name) && !string.IsNullOrEmpty(request.Phone))
            {
                customer = new Customer
                {
                    Name = request.Name,
                    Phone = request.Phone
                };
                await _context.Customers.AddAsync(customer);
            }
            else
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Please select or create a new customer!"
                });
            }

            // Kiểm tra PickUpId và DropOffId, nếu không có thì lấy từ Config
            if (request.PickUpId == null)
            {
                var pickupConfig = await _context.Configs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "default_arival_pickup");
                if (pickupConfig != null)
                {
                    request.PickUpId = int.Parse(pickupConfig.Value);
                }
                else
                {
                    return BadRequest(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Pick-up point configuration not found!"
                    });
                }
            }

            if (request.DropOffId == null)
            {
                var dropoffConfig = await _context.Configs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "default_arival_dropoff");
                if (dropoffConfig != null)
                {
                    request.DropOffId = int.Parse(dropoffConfig.Value);
                }
                else
                {
                    return BadRequest(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Drop-off point configuration not found!"
                    });
                }
            }

            // Kiểm tra PickUpId và DropOffId có hợp lệ trong cơ sở dữ liệu
            if (!await _context.Wards.AnyAsync(w => w.Id == request.PickUpId))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid pick-up point!"
                });
            }

            if (!await _context.Wards.AnyAsync(w => w.Id == request.DropOffId))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid drop-off point!"
                });
            }

            // Tạo Arival và xử lý giá
            var arival = new Arival
            {
                Type = request.Types,
                PickUpId = request.PickUpId,
                PickUpAddress = request.PickUpAddress,
                DropOffId = request.DropOffId,
                DropOffAddress = request.DropOffAddress
            };

            decimal price = 0;

            if (request.Types == "province")
            {
                // Find the Ward based on DropOffId
                var ward = await _context.Wards
                .FirstOrDefaultAsync(w => w.Id == request.DropOffId);

                if (ward != null)
                {
                    // Retrieve District based on ward's district_id
                    var district = await _context.Districts
                    .FirstOrDefaultAsync(d => d.Id == ward.DistrictId);

                    if (district != null)
                    {
                        // Retrieve Province based on district's ProvinceId
                        var province = await _context.Provinces
                        .FirstOrDefaultAsync(p => p.Id == district.ProvinceId);

                        if (province != null)
                        {
                            // Assign the province price
                            price = province.Price.Value;
                        }
                        else
                        {
                            return BadRequest(new
                            {
                                code = CommonErrorCodes.InvalidData,
                                data = (object)null,
                                message = "Province not found for the selected district."
                            });
                        }
                    }
                    else
                    {
                        return BadRequest(new
                        {
                            code = CommonErrorCodes.InvalidData,
                            data = (object)null,
                            message = "District not found for the selected ward."
                        });
                    }
                }
                else
                {
                    return BadRequest(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Ward not found for the selected drop-off point."
                    });
                }
            }

            else if (request.Types == "airport")
            {
                arival.DropOffId = null;
                arival.DropOffAddress = null;

                var airportConfig = await _context.Configs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "airport_price");

                if (airportConfig != null)
                {
                    price = decimal.Parse(airportConfig.Value);
                }
                else
                {
                    return BadRequest(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Airport price config not found."
                    });
                }
            }
            else
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid type for Arival."
                });
            }

            arival.Price = price;


            // Lưu Arival
            await _context.Arivals.AddAsync(arival);
            await _context.SaveChangesAsync();

            // Tạo mới Booking
            var booking = new Booking
            {
                Code = "XG" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CustomerId = customer.Id,
                ArivalId = arival.Id,
                StartAt = DateOnly.FromDateTime(DateTime.UtcNow),
                EndAt = null,
                Count = request.Count,
                Price = arival.Price,
                HasFull = request.HasFull,
                Status = "1",
                InviteId = 0
            };

            await _context.Bookings.AddAsync(booking);
            await _context.SaveChangesAsync();

            var taxi = await FindDriverHelper.FindDriver(booking.Id, 0, _context);

            if (taxi == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Wait for the driver to accept this trip!"
                });
            }
            var driverPhoneNumber = taxi.Phone;
            if (driverPhoneNumber.StartsWith("0"))
            {
                driverPhoneNumber = "+84" + driverPhoneNumber.Substring(1);
            }

            try
            {
                // Khởi tạo Twilio Client
                TwilioClient.Init(configuation["Twilio:AccountSid"], configuation["Twilio:AuthToken"]);

                // Gửi SMS với mã booking đến tài xế
                var message = MessageResource.Create(
                    body: $"Your booking code is: {booking.Code}.",
                    from: new PhoneNumber(configuation["Twilio:PhoneNumber"]),
                    to: new PhoneNumber(driverPhoneNumber)
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = "Failed to send SMS.",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = new { bookingId = booking.Id },
                message = "Trip created successfully and SMS sent to the driver!"
            });
        }

    }
}
