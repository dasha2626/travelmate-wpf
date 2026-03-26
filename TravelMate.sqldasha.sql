BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "dojazdy" (
	"id_dojazdu"	INTEGER,
	"id_podrozy"	INTEGER NOT NULL,
	"id_punktu_start"	INTEGER NOT NULL,
	"id_punktu_koniec"	INTEGER NOT NULL,
	"srodek_transportu"	TEXT NOT NULL,
	"przewoznik"	TEXT,
	"numer_rejsu"	TEXT,
	"data_wyjazdu"	TEXT,
	"data_przyjazdu"	TEXT,
	"cena"	REAL,
	"uwagi"	TEXT,
	"kolejnosc"	INTEGER DEFAULT 0,
	"waluta"	TEXT DEFAULT 'PLN',
	PRIMARY KEY("id_dojazdu" AUTOINCREMENT),
	FOREIGN KEY("id_podrozy") REFERENCES "podroze"("id_podrozy"),
	FOREIGN KEY("id_punktu_koniec") REFERENCES "punkty_dojazdow"("id_punktu"),
	FOREIGN KEY("id_punktu_start") REFERENCES "punkty_dojazdow"("id_punktu")
);
CREATE TABLE IF NOT EXISTS "kategorie" (
	"id_kategorii"	INTEGER,
	"nazwa"	TEXT NOT NULL UNIQUE,
	"ikona"	TEXT NOT NULL,
	"czy_domyslna"	INTEGER DEFAULT 0,
	PRIMARY KEY("id_kategorii" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "kategorie_noclegow" (
	"id_kategorii"	INTEGER,
	"nazwa"	TEXT NOT NULL UNIQUE,
	"ikona"	TEXT NOT NULL,
	"czy_domyslna"	INTEGER DEFAULT 0,
	PRIMARY KEY("id_kategorii" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "noclegi" (
	"id_noclegu"	INTEGER,
	"id_podrozy"	INTEGER NOT NULL,
	"id_punktu"	INTEGER NOT NULL,
	"data_od"	TEXT,
	"data_do"	TEXT,
	"cena"	REAL,
	"ocena"	INTEGER,
	"uwagi"	TEXT,
	"waluta"	TEXT DEFAULT 'PLN',
	PRIMARY KEY("id_noclegu" AUTOINCREMENT),
	FOREIGN KEY("id_podrozy") REFERENCES "podroze"("id_podrozy"),
	FOREIGN KEY("id_punktu") REFERENCES "punkty_noclegowe_old"("id_punktu")
);
CREATE TABLE IF NOT EXISTS "podroze" (
	"id_podrozy"	INTEGER NOT NULL,
	"tytul"	TEXT NOT NULL,
	"miejsce"	TEXT NOT NULL,
	"data_od"	TEXT NOT NULL,
	"data_do"	TEXT NOT NULL,
	"opis"	TEXT,
	"ocena"	INTEGER,
	"id_uzytkownika"	INTEGER,
	"miasto"	TEXT,
	"kraj"	TEXT,
	PRIMARY KEY("id_podrozy" AUTOINCREMENT),
	FOREIGN KEY("id_uzytkownika") REFERENCES "uzytkownicy"("id_uzytkownika")
);
CREATE TABLE IF NOT EXISTS "punkty_dojazdow" (
	"id_punktu"	INTEGER,
	"nazwa"	TEXT NOT NULL,
	"szerokosc"	REAL NOT NULL,
	"dlugosc"	REAL NOT NULL,
	PRIMARY KEY("id_punktu" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "punkty_mapy" (
	"id_punktu"	INTEGER NOT NULL,
	"id_podrozy"	INTEGER NOT NULL,
	"nazwa"	TEXT NOT NULL,
	"szerokosc"	REAL NOT NULL,
	"dlugosc"	REAL NOT NULL,
	"typ"	TEXT CHECK("typ" IN ('odwiedzone', 'planowane')),
	PRIMARY KEY("id_punktu" AUTOINCREMENT),
	FOREIGN KEY("id_podrozy") REFERENCES "podroze"("id_podrozy")
);
CREATE TABLE IF NOT EXISTS "punkty_noclegowe" (
	"id_punktu"	INTEGER,
	"nazwa"	TEXT,
	"szerokosc"	REAL NOT NULL,
	"dlugosc"	REAL NOT NULL,
	"id_kategorii"	INTEGER,
	"adres"	TEXT,
	"opis"	TEXT,
	PRIMARY KEY("id_punktu" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "punkty_zwiedzania" (
	"id_punktu"	INTEGER,
	"nazwa"	TEXT NOT NULL,
	"szerokosc"	REAL NOT NULL,
	"dlugosc"	REAL NOT NULL,
	"typ"	TEXT,
	"id_kategorii"	INTEGER,
	PRIMARY KEY("id_punktu" AUTOINCREMENT),
	FOREIGN KEY("id_kategorii") REFERENCES "kategorie"("id_kategorii")
);
CREATE TABLE IF NOT EXISTS "uzytkownicy" (
	"id_uzytkownika"	INTEGER NOT NULL,
	"login"	TEXT NOT NULL UNIQUE,
	"haslo"	TEXT NOT NULL,
	"email"	TEXT,
	"dom_adres"	TEXT,
	"dom_szerokosc"	REAL,
	"dom_dlugosc"	REAL,
	"dom_kraj"	TEXT,
	"dom_miasto"	TEXT,
	PRIMARY KEY("id_uzytkownika" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "zwiedzanie" (
	"id_zwiedzania"	INTEGER,
	"id_podrozy"	INTEGER NOT NULL,
	"id_punktu"	INTEGER NOT NULL,
	"data"	TEXT,
	"cena"	REAL,
	"ocena"	INTEGER,
	"uwagi"	TEXT,
	"waluta"	TEXT DEFAULT 'PLN',
	PRIMARY KEY("id_zwiedzania" AUTOINCREMENT),
	FOREIGN KEY("id_podrozy") REFERENCES "podroze"("id_podrozy"),
	FOREIGN KEY("id_punktu") REFERENCES "punkty_zwiedzania"("id_punktu")
);
INSERT INTO "kategorie" VALUES (1,'Łyżwy','skiing.png',1);
INSERT INTO "kategorie" VALUES (2,'Góry','mountain.png',1);
INSERT INTO "kategorie" VALUES (3,'Plaża','vacations.png',1);
INSERT INTO "kategorie" VALUES (4,'Zabytek','castle.png',1);
INSERT INTO "kategorie" VALUES (5,'Urbex','factory.png',1);
INSERT INTO "kategorie" VALUES (6,'Park','parkk.png',1);
INSERT INTO "kategorie" VALUES (7,'Kawiarnia','coffee-cup.png',1);
INSERT INTO "kategorie" VALUES (8,'Restauracja','restaurant-building.png',1);
INSERT INTO "kategorie" VALUES (9,'Kino','cinema.png',1);
INSERT INTO "kategorie" VALUES (10,'Muzeum','museum.png',1);
INSERT INTO "kategorie_noclegow" VALUES (1,'Hotel','hotel.png',1);
INSERT INTO "kategorie_noclegow" VALUES (2,'Hostel','hostel.png',1);
INSERT INTO "kategorie_noclegow" VALUES (3,'Camping','tent.png',1);
INSERT INTO "kategorie_noclegow" VALUES (4,'Apartamenty','rent.png',1);
INSERT INTO "kategorie_noclegow" VALUES (5,'Hostel kapsułowy','capsule-hotel.png',1);
INSERT INTO "kategorie_noclegow" VALUES (6,'Schronisko górskie','schronisko.png',1);
INSERT INTO "kategorie_noclegow" VALUES (7,'U znajomych','accomodation.png',1);

COMMIT;
