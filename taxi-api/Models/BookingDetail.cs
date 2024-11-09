﻿using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class BookingDetail
{
    public int Id { get; set; }

    public int? BookingId { get; set; }

    public int? TaxiId { get; set; }

    public string? Status { get; set; }

    public int? Commission { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual Taxy? Taxi { get; set; }
}
