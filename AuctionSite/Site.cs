using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TAP21_22.AlarmClock.Interface;
using TAP21_22_AuctionSite.Interface;

namespace AuctionSite
{
    public class Site: ISite
    {
        public string Name { get; }
        public int Timezone { get; }
        public int SessionExpirationInSeconds { get; }
        public double MinimumBidIncrement { get; }
        public string Connection { get; }
        public IAlarmClock Clock { get; }

        public Site(string name, int timezone, int sessionExpirationInSeconds, double minimumBidIncrement, string connectionString, IAlarmClock clock)
        {
            Name = name;
            Timezone = timezone;
            SessionExpirationInSeconds = sessionExpirationInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
            this.Connection = connectionString;
            this.Clock = clock;
            Clock.InstantiateAlarm(300000).RingingEvent += PulisciSessioni;
        }


        public IEnumerable<IUser> ToyGetUsers()
        {

            var users = new List<IUser>();

            using (var d = new ASDbContext(Connection))
            {
                if (!d.DbSites.Any(s => s.Name.Equals(Name)))
                    throw new AuctionSiteInvalidOperationException(nameof(Name) + "is not existent anymore.");

                foreach (var user in d.DbUsers.Where(s => s.SiteName.Equals(Name)))
                {
                    users.Add(new User(user.Username, Name, Connection, Clock));
                }
                return users;
            }
        }

        public IEnumerable<ISession> ToyGetSessions()
        {

            var retrievedSessions = new List<ISession>();

            using (var d = new ASDbContext(Connection))
            {
                if (!d.DbSites.Any(s => s.Name.Equals(Name)))
                    throw new AuctionSiteInvalidOperationException(nameof(Name) + "is not existent anymore.");

                foreach (var session in d.DbSessions.Where(s => s.SiteName.Equals(Name)&& DateTime.Compare(s.ValidUntil, Clock.Now)>0))
                   
                    retrievedSessions.Add(new Session(session.Id,
                        new User(session.Username, Name, Connection, Clock), Connection,
                        Clock, Name));
            }
            return retrievedSessions;

        }

        public IEnumerable<IAuction> ToyGetAuctions(bool onlyNotEnded)
        {

            var retrievedAuctions = new List<IAuction>();
            using (var d = new ASDbContext(Connection))
            {
                if (!d.DbSites.Any(s => s.Name.Equals(Name)))
                    throw new AuctionSiteInvalidOperationException(nameof(Name) + "is not existent anymore.");
                if (!onlyNotEnded)
                {
                    foreach (var siteAuction in d.DbAuctions.Where(a => a.SiteName.Equals(Name)))

                        retrievedAuctions.Add(new Auction(siteAuction.DbAuctionId,
                            new User(siteAuction.Seller, Name, Connection, Clock),
                            siteAuction.Description, siteAuction.EndsOn, siteAuction.SiteName, Connection,
                            Clock));
                }
                else
                {
                    foreach (var siteAuction in d.DbAuctions.Where(a => a.SiteName.Equals(Name)))

                        if (Clock.Now < siteAuction.EndsOn)

                            retrievedAuctions.Add(new Auction(siteAuction.DbAuctionId,
                                new User(siteAuction.Seller, Name, Connection, Clock),
                                siteAuction.Description, siteAuction.EndsOn, siteAuction.SiteName, Connection,
                                Clock));
                }
            }
            return retrievedAuctions;
        }

        public ISession Login(string username, string password)
        {
            ControllaUtente_Password(username, password);
            using (var d = new ASDbContext(Connection))
            {
                var site = d.DbSites.FirstOrDefault(s => s.Name.Equals(Name));
                if (site is null)
                    throw new AuctionSiteInvalidOperationException("The site doesn't exist anymore.");

                var user = d.DbUsers.FirstOrDefault(u => u.Username.Equals(username) && u.SiteName.Equals(Name));

                if (user is null)
                    return null;

                if (!CheckPasswordHashed(password, user.Password))
                    return null;
                //Sessione creata concatenando username, nome del sito e la parola session cercando di creare una stringa unique
                var sessionId = username + Name + "session";
                var session = d.DbSessions.FirstOrDefault(s => s.Id.Equals(sessionId));
                if (session != null && DateTime.Compare(session.ValidUntil, Clock.Now)>0)
                {
                    session.ValidUntil = Clock.Now.AddSeconds(SessionExpirationInSeconds);
                    d.SaveChanges();
                    Debug.WriteLine(session.Id + " " + session.ValidUntil + " :changeLoginSuccessfull");
                    return new Session(session.Id,
                        new User(session.Username, Name, Connection, Clock),
                        Connection, Clock, Name);
                }
                //Se non esiste la sessione oppure non è valida, viene restituita una nuova sessione
                
                if (session != null)
                {
                    session.Id = sessionId;
                    session.ValidUntil = Clock.Now.AddSeconds(SessionExpirationInSeconds);
                    session.Username = username;
                    session.TimeZone = Timezone;
                    session.SiteName = Name;
                    session.DbSiteId = site.DbSiteId;
                    session.DbSite = site;
                    
                    d.DbSessions.Update(session);
                    d.SaveChanges();


                    return new Session(session.Id,
                        new User(session.Username, Name, Connection, Clock), Connection,
                        Clock, Name);

                }

                session = new DbSession()
                    {
                        Id = sessionId,
                        ValidUntil = Clock.Now.AddSeconds(SessionExpirationInSeconds),
                        Username = username,
                        TimeZone = Timezone,
                        SiteName = Name,
                        DbSiteId = site.DbSiteId,
                        DbSite = site
                    };
                d.DbSessions.Add(session);
                d.SaveChanges();

                return new Session(session.Id,
                    new User(session.Username, Name, Connection, Clock), Connection,
                    Clock, Name);
            }
        }




        public void CreateUser(string username, string password)
        {
            ControllaUtente_Password(username, password);
            var hashedpwd = Hashedpassword(password);
            using (var d=new ASDbContext(Connection))
            {
                var site = d.DbSites.FirstOrDefault(s => s.Name.Equals(Name));
                if (site is null)
                    throw new AuctionSiteInvalidOperationException("The site doesn't exist anymore.");
                if (d.DbUsers.Any(user => user.Username.Equals(username) && user.SiteName.Equals(Name)))
                    throw new AuctionSiteNameAlreadyInUseException(nameof(username) + " already in use.");

                d.DbUsers.Add(new DbUser()
                {
                    Username = username,
                    SiteName = Name,
                    Password = hashedpwd,
                    DbSiteId = site.DbSiteId
                });

                d.SaveChanges();
               

            }
        }

        public void Delete()
        {
            using (var d = new ASDbContext(Connection))
            {
                var site = d.DbSites.FirstOrDefault(s => s.Name.Equals(Name));
                if (site is null)
                    throw new AuctionSiteInvalidOperationException("The site doesn't exist anymore.");
                d.DbSites.Remove(site);
                d.SaveChanges();

            }
        }

        public DateTime Now()
        {
            return Clock.Now;
        }
        private IEnumerable<ISession> GetExpiredSessions()
        {

            var retrievedSessions = new List<ISession>();

            using (var d = new ASDbContext(Connection))
            {
                if (!d.DbSites.Any(s => s.Name.Equals(Name)))
                    throw new AuctionSiteInvalidOperationException(nameof(Name) + "is not existent anymore.");

                foreach (var session in d.DbSessions.Where(s => s.SiteName.Equals(Name) && DateTime.Compare(s.ValidUntil,Clock.Now)<0))

                    retrievedSessions.Add(new Session(session.Id,
                        new User(session.Username, Name, Connection, Clock), Connection,
                        Clock, Name));
            }
            return retrievedSessions;

        }
        //Elimina le sessioni scadute
        private void PulisciSessioni()
        {
            using (var d = new ASDbContext(Connection))
            {
                if (!d.DbSites.Any(s => s.Name.Equals(Name)))
                    throw new InvalidOperationException(nameof(Name) + "is not existent anymore.");
                foreach (var sessionEntry in GetExpiredSessions())
                {
                    d.DbSessions.Remove(d.DbSessions.First(s => s.Id.Equals(sessionEntry.Id)));
                    d.SaveChanges();
                }
            }
        }

        //Controlla che i parametri username e password non siano nulli e che rispettino le specifiche di DomainConstraints
        private void ControllaUtente_Password(string username, string password)
        {
            if (username is null)
                throw new AuctionSiteArgumentNullException(nameof(username) + " cannot be null.");
            if (password is null)
                throw new AuctionSiteArgumentNullException(nameof(password) + " cannot be null.");
            if (username.Length < DomainConstraints.MinUserName)
                throw new AuctionSiteArgumentException(nameof(username) + " too short.");
            if (username.Length > DomainConstraints.MaxUserName)
                throw new AuctionSiteArgumentException(nameof(username) + " too long.");
            if (password.Length < DomainConstraints.MinUserPassword)
                throw new AuctionSiteArgumentException(nameof(password) + " too short.");
        }

        //Crea una password criptata utilizzando il protocollo RNGCRYPTO e rfc2898 contenuti in System.Cryptography
        private string Hashedpassword(string password)
        {
            byte[] salt;
            //STEP 1:Crea un salt di valori pseudocasuali di 16 byte
            new RNGCryptoServiceProvider().GetBytes(salt = new byte[16]);
            //STEP2:Cripta la password con il salt per 10 iterazioni
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10);
            //STEP 4:Crea un hash con la password criptata
            byte[] hash = pbkdf2.GetBytes(20);
            //STEP 5:Si concatenano salt e hash per salvare il risultato delle operazioni sopra sul database
            byte[] hashBytes = new byte[36];
            //
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 20);
            //Password dell'utente che si andrà a salvare sul db
            string savedPasswordHash = Convert.ToBase64String(hashBytes);
            return savedPasswordHash;
        }

        //Funzione di supporto per il metodo Login, controlla che la password inserita dall'utente durante il login corrisponda
        //a quella sul db
        private bool CheckPasswordHashed(string password, string savedPasswordHash)
        {
            //Estrae i bytes
            byte[] hashBytes = Convert.FromBase64String(savedPasswordHash);
            //Recupera il salt dai bytes estratti dalla password
            byte[] salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);
            //Viene generato l'hash dalla password inserita dall'utente
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10);
            byte[] hash = pbkdf2.GetBytes(20);
            //Comparazione dei risultati
            for (int i = 0; i < 20; i++)
            {
                if (hashBytes[i + 16] != hash[i])
                    return false;
            }

            return true;

        }
        //Override di GetHashCode
        public override int GetHashCode()
        {
            return 31 * 17 + HashCode.Combine(Name);
        }
        //Override di Equals
        public override bool Equals(object obj)
        {
            var other = obj as Site;
            if (other == null) return false;
            return (Name == other.Name);
        }
        //Overload di ==
        public static bool operator ==(Site left, Site right)
        {
            if (left is null || right is null) return false;
            return left.Equals(right);
        }
        //Overload di !=
        public static bool operator !=(Site left, Site right)
        {
            if (left is null || right is null) return true;
            return !left.Equals(right);
        }

    }
}
