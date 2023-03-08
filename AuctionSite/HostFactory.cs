using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TAP21_22_AuctionSite.Interface;
using TAP21_22.AlarmClock.Interface;

namespace AuctionSite
{
    public class HostFactory : IHostFactory
    {
        public void CreateHost(string connectionString)
        {
            CheckConnection(connectionString);
            try
            {
                using (var db = new ASDbContext(connectionString))
                {
                    db.Database.EnsureDeleted();
                    db.Database.EnsureCreated();
                    db.SaveChanges();
                }
            }
            catch (Exception e)
            {
                throw new AuctionSiteUnavailableDbException("Unexpected Error", e);
            }
          
        }

        public IHost LoadHost(string connectionString, IAlarmClockFactory alarmClockFactory)
        {
            CheckConnection(connectionString);
            if (alarmClockFactory == null)
                throw new AuctionSiteArgumentNullException(nameof(alarmClockFactory) + " cannot be null.");
            return new Host(connectionString, alarmClockFactory);
        }





        private void CheckConnection(string connectionString)
        {
            if (connectionString is null)
                throw new AuctionSiteArgumentNullException(nameof(connectionString) + " cannot be null.");
            try
            {
                using (var db = new ASDbContext(connectionString))
                {
                    db.Database.EnsureCreated();
                    db.Database.OpenConnection();
                    db.Database.CloseConnection();
                }
            }
            catch (Exception e)
            {
                throw new AuctionSiteUnavailableDbException(e.Message, e);
            }

        }
    }




}
