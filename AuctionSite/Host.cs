using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAP21_22.AlarmClock.Interface;
using TAP21_22_AuctionSite.Interface;

namespace AuctionSite
{
    public class Host: IHost
    {
        
        private readonly IAlarmClockFactory _clockfactory;
        private readonly string _connection;

        public Host(string connection, IAlarmClockFactory clockfactory)
        {
            
            _clockfactory = clockfactory;
            _connection = connection;
        }

        public IEnumerable<(string Name, int TimeZone)> GetSiteInfos()
        {
            
            using (var d = new ASDbContext(_connection))
            {
                var siteInfos = d.DbSites.Select(s => new {s.Name, s.TimeZone}).AsEnumerable()
                    .Select(s => (s.Name, s.TimeZone)).ToList();
                return siteInfos.ToList();
            }
        }

        public void CreateSite(string name, int timezone, int sessionExpirationTimeInSeconds, double minimumBidIncrement)
        {
            Checkname(name);
            if (timezone < DomainConstraints.MinTimeZone)
                throw new AuctionSiteArgumentOutOfRangeException(nameof(timezone) + " is too small.");
            if (timezone > DomainConstraints.MaxTimeZone)
                throw new AuctionSiteArgumentOutOfRangeException(nameof(timezone) + " is too big");
            if (minimumBidIncrement <= 0)
                throw new AuctionSiteArgumentOutOfRangeException(nameof(minimumBidIncrement) + " must be positive.");
            if (sessionExpirationTimeInSeconds <= 0)
                throw new AuctionSiteArgumentOutOfRangeException(nameof(sessionExpirationTimeInSeconds) + " must be positive.");
            try
            {
                using (var db = new ASDbContext(_connection))
                {


                    if (db.DbSites.Any(site => site.Name.Equals(name)))
                        throw new AuctionSiteNameAlreadyInUseException(nameof(name) + "is already present");

                    db.DbSites.Add(new DbSite()
                    {
                        Name = name,
                        TimeZone = timezone,
                        SessionExpirationTimeinSeconds = sessionExpirationTimeInSeconds,
                        MinBidIncrement = minimumBidIncrement
                    });
                    db.SaveChanges();
                }
            }
            catch (AuctionSiteNameAlreadyInUseException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new AuctionSiteUnavailableDbException("Unexpected Error",e);
            }
        }

        public ISite LoadSite(string name)
        {
            Checkname(name);
            try
            {
                using (var db = new ASDbContext(_connection))
                {
                    var site = db.DbSites.FirstOrDefault(s => s.Name.Equals(name));
                    if (site is null)
                        throw new AuctionSiteInexistentNameException(nameof(name) + " is not existent.");
                    if(site.TimeZone!=_clockfactory.InstantiateAlarmClock(site.TimeZone).Timezone)
                        throw new AuctionSiteArgumentException("Site's timezone is inconsistent with Clock's timezone");
                    return new Site(site.Name, site.TimeZone,
                        site.SessionExpirationTimeinSeconds, site.MinBidIncrement, _connection,
                        _clockfactory.InstantiateAlarmClock(site.TimeZone));
                }
            }
            catch (AuctionSiteInexistentNameException)
            {
                throw;
            }
            catch (AuctionSiteArgumentException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new AuctionSiteUnavailableDbException("Unexpected Error",e);
            }
        }


        private void Checkname(string name)
        {
            if (name is null)
                throw new AuctionSiteArgumentNullException(nameof(name) + "cannot be null.");
            if (name.Length < DomainConstraints.MinSiteName)
                throw new AuctionSiteArgumentException(nameof(name) + " is too short.");
            if (name.Length > DomainConstraints.MaxSiteName)
                throw new AuctionSiteArgumentException(nameof(name) + " is too long.");

        }

    }

    







}
