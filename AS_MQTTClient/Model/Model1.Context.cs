﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace AS_MQTTClient.Model
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class AS_MQTTClientEntities : DbContext
    {
        public AS_MQTTClientEntities()
            : base("name=AS_MQTTClientEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<Data_Analog_test> Data_Analog_test { get; set; }
        public virtual DbSet<Data_Modbus_test> Data_Modbus_test { get; set; }
        public virtual DbSet<Data_StateRelay> Data_StateRelay { get; set; }
        public virtual DbSet<UserRole> UserRoles { get; set; }
        public virtual DbSet<User> Users { get; set; }
    }
}
