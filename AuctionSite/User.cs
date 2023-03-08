using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAP21_22.AlarmClock.Interface;
using TAP21_22_AuctionSite.Interface;

namespace AuctionSite
{
    public class User: IUser
    {
        public string Username { get; }
        private readonly string _sitename;
        private readonly string _connection;
        private readonly IAlarmClock _clock;
        public User(string username, string sitename, string connection, IAlarmClock clock)
        {
            Username = username;
            _sitename = sitename;
            _connection = connection;
            _clock = clock;

        }

        public IEnumerable<IAuction> WonAuctions()
        {
            var auctions = new List<IAuction>();
            using (var d = new ASDbContext(_connection))
            {
                if (!d.DbUsers.Any(u => u.SiteName.Equals(_sitename) && u.Username.Equals(Username)))
                    throw new AuctionSiteInvalidOperationException(nameof(Username) + "is not existent anymore.");
                foreach (var auction in d.DbAuctions.Where(a => a.Winner.Equals(Username) && a.SiteName.Equals(_sitename)))
                {
                    if (auction.EndsOn < _clock.Now)
                        auctions.Add(new Auction(auction.DbAuctionId,
                            new User(auction.Seller, auction.SiteName, _connection, _clock),
                            auction.Description, auction.EndsOn, auction.SiteName, _connection, _clock));
                }
            }

            return auctions;
        }

        public void Delete()
        {
            using (var d = new ASDbContext(_connection))
            {
                if (d.DbAuctions.Any(a => a.Seller.Equals(Username) && a.SiteName.Equals(_sitename) && a.EndsOn < _clock.Now))
                    throw new AuctionSiteInvalidOperationException("cannot delete a user who is the seller of a not ended auction");

                if (d.DbAuctions.Any(a => a.Winner.Equals(Username) && a.SiteName.Equals(_sitename) && a.EndsOn < _clock.Now))
                    throw new AuctionSiteInvalidOperationException("cannot delete a user who is the winner of a not ended auction");

                var user = d.DbUsers.FirstOrDefault(u => u.Username.Equals(Username) && u.SiteName.Equals(_sitename));

                if (user is null)
                    throw new AuctionSiteInvalidOperationException("the user does not exist in the context");

                d.DbUsers.Remove(user);
                d.DbAuctions.RemoveRange(d.DbAuctions.Where(a =>
                    a.Seller.Equals(Username) && a.SiteName.Equals(_sitename)));

                foreach (var auction in d.DbAuctions.Where(a =>
                    a.Winner.Equals(Username) && a.SiteName.Equals(_sitename)))
                {
                    auction.Winner = null;
                }

                d.SaveChanges();
            }
        }

        //Override di GetHashCode
        public override int GetHashCode()
        {
            return 31 * 17 + HashCode.Combine(Username);
        }
        //Override di Equals
        public override bool Equals(object obj)
        {
            var other = obj as User;
            if (other == null) return false;
            return (Username == other.Username && _sitename == other._sitename);
        }
        //Overload di ==
        public static bool operator ==(User left, User right)
        {
            if (left is null || right is null) return false;
            return left.Equals(right);
        }
        //Overload di !=
        public static bool operator !=(User left, User right)
        {
            if (left is null || right is null) return true;
            return !left.Equals(right);
        }
    }
}
