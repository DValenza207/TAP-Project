using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAP21_22.AlarmClock.Interface;
using TAP21_22_AuctionSite.Interface;

namespace AuctionSite
{
    public class Auction: IAuction
    {
        public int Id { get; }
        public IUser Seller { get; }
        public string Description { get; }
        public DateTime EndsOn { get; }

        private readonly string _sitename;
        private readonly string _connection;
        private readonly IAlarmClock _clock;

        public Auction(int id, IUser seller, string description, DateTime ends, string sitename, string connection, IAlarmClock clock)
        {
            Id = id;
            Seller = seller;
            Description = description;
            EndsOn = ends;
            _sitename = sitename;
            _connection = connection;
            _clock = clock;

        }

        public IUser? CurrentWinner()
        {
            using (var d = new ASDbContext(_connection))
            {
                var auction = d.DbAuctions.FirstOrDefault(a => a.DbAuctionId.Equals(Id));
                if (auction is null)
                    return null;
                if (auction.MaximumOffer == 0.0)
                    return null;
                if (auction.EndsOn < _clock.Now && auction.Winner is null)
                    return null;
                var user = d.DbUsers.FirstOrDefault(u => u.Username.Equals(auction.Winner));
                if (user is null)
                    throw new AuctionSiteInvalidOperationException();
                return new User(user.Username, user.SiteName, _connection, _clock);

            }
        }

        public double CurrentPrice()
        {
            using (var d = new ASDbContext(_connection))
            {
                var auction = d.DbAuctions.FirstOrDefault(a => a.DbAuctionId.Equals(Id));
                if (auction is null)
                    throw new AuctionSiteInvalidOperationException("the auction is inexistent.");
                return auction.ActualPrice;
            }
        }

        public void Delete()
        {
            using (var d = new ASDbContext(_connection))
            {
                var auction = d.DbAuctions.FirstOrDefault(a => a.DbAuctionId.Equals(Id));
                if (auction is null)
                    throw new AuctionSiteInvalidOperationException("the auction is inexistent.");
                d.DbAuctions.Remove(auction);
                d.SaveChanges();
            }
        }

        public bool Bid(ISession session, double offer)
        {
            if (session is null)
                throw new AuctionSiteArgumentNullException(nameof(session) + " is null");
            if (offer < 0)
                throw new AuctionSiteArgumentOutOfRangeException(nameof(offer) + " is negative");
            if (session.User.Username == Seller.Username)
                throw new AuctionSiteArgumentException(nameof(Seller) + "is the logged user");
            if(DateTime.Compare(session.ValidUntil,_clock.Now)<0)
                throw new AuctionSiteArgumentException();
            using (var d = new ASDbContext(_connection))
            {
                var BidAccepted = true;
                var auction = d.DbAuctions.FirstOrDefault(a => a.DbAuctionId.Equals(Id));

                if (EndsOn < _clock.Now || auction is null)
                    throw new AuctionSiteInvalidOperationException("Auction is finished");

                var siteuserlogged = d.DbUsers.FirstOrDefault(u => u.Username.Equals(session.User.Username));

                if (siteuserlogged is null)
                    throw new AuctionSiteInvalidOperationException("the site of the user logged is not existent.");

                var siteseller = d.DbUsers.FirstOrDefault(u => u.Username.Equals(Seller.Username));

                if (siteseller is null)
                    throw new AuctionSiteInvalidOperationException("the site of the seller is not existent.");

                if (siteuserlogged.SiteName != siteseller.SiteName)
                    throw new AuctionSiteArgumentException("the sitename of " + nameof(session.User) + "is not the same of " + nameof(Seller));
                var site = d.DbSites.FirstOrDefault(s => s.Name.Equals(_sitename));

                if (site is null)
                    throw new AuctionSiteInvalidOperationException("site does not exist");

                //Primo caso falso:L'offerente è già l'attuale vincitore e l'offerta è più bassa dell'offerta massima incrementata da MinBidIncrement
                if (session.User.Username == auction.Winner && offer < (auction.MaximumOffer + site.MinBidIncrement))
                    BidAccepted = false;
                //Secondo caso falso:L'offerente non è l'attuale vincitore e l'offerta è più bassa del prezzo attuale
                if (session.User.Username != auction.Winner && offer < auction.ActualPrice)
                    BidAccepted = false;
                //Terzo caso falso:L'offerente non è l'attuale vincitore, l'offerta è più bassa del prezzo attuale incrementato da MinBidIncrement
                //e questa non è la prima offerta.
                if (auction.MaximumOffer != 0.0 && offer < (auction.ActualPrice + site.MinBidIncrement) &&
                    session.User.Username != auction.Winner)
                    BidAccepted = false;
                //Primo caso vero:Se questa è la prima offerta, allora l'offerta massima è settata come offer,
                //il prezzo attuale non è cambiato (rimane il prezzo di partenza) e l'offerente diventa l'attuale vincitore.
                if (auction.MaximumOffer == 0.0)
                {
                    auction.MaximumOffer = offer;
                    auction.Winner = session.User.Username;
                }
                //Secondo caso vero:
                //Se l'offerente stava già vincendo questa asta, l'offerta massima è settata su offer,
                //il prezzo attuale e l'attuale vincitore non hanno subìto variazioni.
                if (session.User.Username == auction.Winner)
                    auction.MaximumOffer = offer;
                // True Case 3:
                //Se questa NON è la prima offerta, l'offerente NON è l'attuale vincitore e l'offerta è più alta dell'attuale offerta massima,
                //allora il prezzo attuale è settato al minimo tra l'offerta e MinBidIncrement,
                //l'offerta massima viene settata a offer e l'offerente diventa l'attuale vincitore.
                if (auction.MaximumOffer != 0.0 && session.User.Username != auction.Winner &&
                    offer > auction.MaximumOffer)
                {
                    if (offer > auction.MaximumOffer + site.MinBidIncrement)
                        auction.ActualPrice = auction.MaximumOffer + site.MinBidIncrement;
                    else
                        auction.ActualPrice = offer;

                    auction.MaximumOffer = offer;
                    auction.Winner = session.User.Username;
                }

                //Quarto caso vero:
                //Se questa NON è la prima offerta, l'offerente NON è l'attuale vincitore e l'offerta NON è più alta dell'attuale offerta massima,
                //allora il prezzo attuale è settato al minimo tra l'offerta massima e l'offerta incrementata da MinBidIncrement
                //e l'attuale vincitore non cambia.
                if (auction.MaximumOffer != 0.0 && session.User.Username != auction.Winner &&
                    offer < auction.MaximumOffer)
                {
                    if (auction.MaximumOffer > offer + site.MinBidIncrement)
                        auction.ActualPrice = offer + site.MinBidIncrement;
                    else
                        auction.ActualPrice = auction.MaximumOffer;
                }

                var sessionDb = d.DbSessions.FirstOrDefault(s => s.Id.Equals(session.Id));

                if (sessionDb is null)
                    throw new AuctionSiteInvalidOperationException("the session is not existent.");

                sessionDb.ValidUntil = _clock.Now.AddSeconds(site.SessionExpirationTimeinSeconds);

                d.SaveChanges();
                return BidAccepted;

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
            var other = obj as Auction;
            if (other == null) return false;
            return (Id == other.Id);
        }
        //Overload di ==
        public static bool operator ==(Auction left, Auction right)
        {
            if (left is null || right is null) return false;
            return left.Equals(right);
        }
        //Overload di !=
        public static bool operator !=(Auction left, Auction right)
        {
            if (left is null || right is null) return true;
            return !left.Equals(right);
        }


    }
}
