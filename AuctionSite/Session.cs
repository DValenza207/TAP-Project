using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAP21_22.AlarmClock.Interface;
using TAP21_22_AuctionSite.Interface;

namespace AuctionSite
{
    public class Session: ISession
    {
        public string Id { get; }
        public DateTime ValidUntil
        {
            get
            {
                var valid = ValidityControl();
                return valid;
            }

        }
        public IUser User { get; }

        private readonly string _connection;
        private readonly IAlarmClock _clock;
        private readonly string _sitename;

        public Session(string id, IUser user, string connection, IAlarmClock clock, string sitename)
        {
            Id = id;
            User = user;
            _connection = connection;
            _clock = clock;
            _sitename = sitename;
        }

        public void Logout()
        {
            if(!(ValidityControl()>_clock.Now))
                throw new AuctionSiteInvalidOperationException();
            ModifyValidity(_clock.Now.AddSeconds(-1));

        }

        public IAuction CreateAuction(string description, DateTime endsOn, double startingPrice)
        {
            if (description is null)
                throw new AuctionSiteArgumentNullException(nameof(description) + "couldn't be null");
            if (description.Length == 0)
                throw new AuctionSiteArgumentException(nameof(description) + "is empty");
            if (startingPrice < 0)
                throw new AuctionSiteArgumentOutOfRangeException(nameof(startingPrice) + "is negative");
            if (endsOn < _clock.Now)
                throw new AuctionSiteUnavailableTimeMachineException("EndsOn precedes current time");

            using (var db = new ASDbContext(_connection))
            {
                var siteid = db.DbSites.FirstOrDefault(s => s.Name.Equals(_sitename));

                if (siteid is null)
                    throw new AuctionSiteInvalidOperationException("the site of the session is not existent.");

                var sessionvalidUntil = db.DbSessions.FirstOrDefault(s => s.Id.Equals(Id));

                //Se la sessione non è valida o non esiste
                if (sessionvalidUntil is null || sessionvalidUntil.ValidUntil < _clock.Now)
                    throw new AuctionSiteInvalidOperationException("sessione non esitente");

                var dbAuction = db.DbAuctions.Add(new DbAuction()
                {
                    Description = description,
                    EndsOn = endsOn,
                    ActualPrice = startingPrice,
                    MaximumOffer = 0,
                    Seller = User.Username,
                    SiteName = _sitename,
                    DbSiteId = siteid.DbSiteId
                });
                ModifyValidity(_clock.Now.AddSeconds(db.DbSites.Single(s => s.Name.Equals(_sitename)).SessionExpirationTimeinSeconds));
                db.SaveChanges();

                var auctionId = db.DbAuctions.Max(s => s.DbAuctionId);
                return new Auction(auctionId, new User(User.Username, _sitename, _connection, _clock),
                    description, endsOn, _sitename, _connection, _clock);
            }

        }
        private void ModifyValidity(DateTime value)
        {
            using (var db = new ASDbContext(_connection))
            {
                var retrievedSession = db.DbSessions.FirstOrDefault(s => s.Id.Equals(Id));
                if (retrievedSession is null)
                    throw new AuctionSiteInvalidOperationException("Session not found.");
                retrievedSession.ValidUntil = value;
                db.SaveChanges();
            }
        }

        private DateTime ValidityControl()
        {
            using (var db = new ASDbContext(_connection))
            {
                var retrievedSession = db.DbSessions.FirstOrDefault(s => s.Id.Equals(Id));
                if (retrievedSession is null)
                    throw new AuctionSiteInvalidOperationException("Session not found.");
                return retrievedSession.ValidUntil;
            }
        }

        //Override di GetHashCode
        public override int GetHashCode()
        {
            return 31 * 17 + HashCode.Combine(Id);
        }
        //Override di Equals
        public override bool Equals(object obj)
        {
            var other = obj as Session;
            if (other == null) return false;
            return (Id == other.Id);
        }
        //Overload di ==
        public static bool operator ==(Session left, Session right)
        {
            if (left is null || right is null) return false;
            return left.Equals(right);
        }
        //Overload di !=
        public static bool operator !=(Session left, Session right)
        {
            if (left is null || right is null) return true;
            return !left.Equals(right);
        }

    }
}
