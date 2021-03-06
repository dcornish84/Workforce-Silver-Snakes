﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Workforce_Silver_Snakes.Models
{
    public class Product
    {
        public int Id { get; set; }
        public DateTime DateAdded { get; set; }
        public int ProductTypeId { get; set; }
        public int CustomerId { get; set; }
        public string Price { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
}
