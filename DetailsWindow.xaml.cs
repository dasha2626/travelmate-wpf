using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;   
using System.Net.Http;          
using System.Text.Json;         
using System.IO;



namespace TravelMate
{
    public partial class DetailsWindow : Window
    {
        private int idPodrozy;
     
        private string loginUzytkownika;
        public DetailsWindow(int idPodrozy, string loginUzytkownika)
        {
            InitializeComponent();
            this.idPodrozy = idPodrozy;
            this.loginUzytkownika = loginUzytkownika; 
            LoadData();

            var stats = PobierzStatystykiPodrozy(idPodrozy);
            DataContext = stats;

            _ = LoadRatesAsync().ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    LoadData();
                    stats = PobierzStatystykiPodrozy(idPodrozy);
                    DataContext = stats;
                });
            });
        }

        private async void LoadData()
        {
            await LoadRatesAsync();
            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                var cmdPodroz = new SQLiteCommand("SELECT * FROM podroze WHERE id_podrozy=@id", conn);
                cmdPodroz.Parameters.AddWithValue("@id", idPodrozy);
                var reader = cmdPodroz.ExecuteReader();
                if (reader.Read())
                {
                    var podroz = new
                    {
                        Tytul = reader["tytul"].ToString(),
                        Miasto = reader["miasto"].ToString(),
                        Kraj = reader["kraj"].ToString(),
                        DataOd = DateTime.Parse(reader["data_od"].ToString()),
                        DataDo = DateTime.Parse(reader["data_do"].ToString()),
                        Opis = reader["opis"].ToString()
                    };

                    OgolneInfoGrid.DataContext = podroz;
                }


                var zwiedzanie = new List<dynamic>();
                var cmdZwiedzanie = new SQLiteCommand(@"
            SELECT z.*, p.nazwa, p.typ, k.nazwa AS kategoria
            FROM zwiedzanie z
            LEFT JOIN punkty_zwiedzania p ON z.id_punktu=p.id_punktu
            LEFT JOIN kategorie k ON p.id_kategorii=k.id_kategorii
            WHERE z.id_podrozy=@id
            ORDER BY z.data ASC", conn);
                cmdZwiedzanie.Parameters.AddWithValue("@id", idPodrozy);
                reader = cmdZwiedzanie.ExecuteReader();
                while (reader.Read())
                {
                    double cenaPLN = ConvertToPLN(reader["cena"] != DBNull.Value ? Convert.ToDouble(reader["cena"]) : 0, reader["waluta"].ToString());
                    int ocena = reader["ocena"] != DBNull.Value ? Convert.ToInt32(reader["ocena"]) : 0;
                    zwiedzanie.Add(new
                    {
                        nazwa = reader["nazwa"].ToString(),
                        typ = reader["kategoria"] != DBNull.Value ? reader["kategoria"].ToString() : reader["typ"].ToString(),
                        data = reader["data"].ToString(),
                        cena = cenaPLN.ToString("N2") + " PLN",
                        ocena = ocena,
                        uwagi = reader["uwagi"].ToString()
                    });
                }
                icZwiedzanie.ItemsSource = zwiedzanie;
                reader.Close();

                var noclegi = new List<dynamic>();
                var cmdNoclegi = new SQLiteCommand(@"
            SELECT n.*, pn.nazwa, pn.adres, kn.nazwa AS kategoria
            FROM noclegi n
            LEFT JOIN punkty_noclegowe pn ON n.id_punktu=pn.id_punktu
            LEFT JOIN kategorie_noclegow kn ON pn.id_kategorii=kn.id_kategorii
            WHERE n.id_podrozy=@id
            ORDER BY n.data_od ASC", conn);
                cmdNoclegi.Parameters.AddWithValue("@id", idPodrozy);
                reader = cmdNoclegi.ExecuteReader();
                while (reader.Read())
                {
                    double cenaPLN = ConvertToPLN(reader["cena"] != DBNull.Value ? Convert.ToDouble(reader["cena"]) : 0, reader["waluta"].ToString());
                    int ocena = reader["ocena"] != DBNull.Value ? Convert.ToInt32(reader["ocena"]) : 0;
                    noclegi.Add(new
                    {
                        nazwa = reader["nazwa"].ToString(),
                        adres = reader["adres"].ToString(),
                        kategoria = reader["kategoria"].ToString(),
                        data_od = reader["data_od"].ToString(),
                        data_do = reader["data_do"].ToString(),
                        cena = cenaPLN.ToString("N2") + " PLN",
                        ocena = ocena,
                        uwagi = reader["uwagi"].ToString()
                    });
                }
                icNoclegi.ItemsSource = noclegi;
                reader.Close();

                var dojazdy = new List<dynamic>();
                var cmdDojazdy = new SQLiteCommand(@"
            SELECT d.*, ps.nazwa AS start, pk.nazwa AS cel
            FROM dojazdy d
            LEFT JOIN punkty_dojazdow ps ON d.id_punktu_start=ps.id_punktu
            LEFT JOIN punkty_dojazdow pk ON d.id_punktu_koniec=pk.id_punktu
            WHERE d.id_podrozy=@id
            ORDER BY d.data_wyjazdu ASC", conn);
                cmdDojazdy.Parameters.AddWithValue("@id", idPodrozy);
                reader = cmdDojazdy.ExecuteReader();
                while (reader.Read())
                {
                    double cenaPLN = ConvertToPLN(reader["cena"] != DBNull.Value ? Convert.ToDouble(reader["cena"]) : 0, reader["waluta"].ToString());
                    string numerRejsu = reader["numer_rejsu"] != DBNull.Value ? reader["numer_rejsu"].ToString() : "";
                    dojazdy.Add(new
                    {
                        start = reader["start"].ToString(),
                        cel = reader["cel"].ToString(),
                        transport = reader["srodek_transportu"].ToString(),
                        przewoznik = reader["przewoznik"].ToString(),
                        numer_rejsu = numerRejsu,
                        data_wyjazdu = reader["data_wyjazdu"].ToString(),
                        data_przyjazdu = reader["data_przyjazdu"].ToString(),
                        uwagi = reader["uwagi"].ToString(),
                        cena = cenaPLN.ToString("N2") + " PLN"
                    });
                }
                icDojazdy.ItemsSource = dojazdy;
                reader.Close();
            }
        }






        private void StyleTable(Table table)
        {
            table.Borders.Width = 0.5;
            table.Format.Alignment = ParagraphAlignment.Center;

            foreach (Row row in table.Rows)
            {
                row.TopPadding = Unit.FromMillimeter(2);
                row.BottomPadding = Unit.FromMillimeter(2);

                foreach (Cell cell in row.Cells)
                {
                    cell.VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;
                    cell.Format.Alignment = ParagraphAlignment.Left;
                }
            }
        }

        private void BtnPDF_Click(object sender, RoutedEventArgs e)
        {
            var stats = PobierzStatystykiPodrozy(idPodrozy);

            Document doc = new Document();
            doc.Info.Title = this.Title;

            Section section = doc.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.TopMargin = Unit.FromCentimeter(2.5);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(2.5);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(2.5);
            section.PageSetup.RightMargin = Unit.FromCentimeter(2.5);

           
            var footer = section.Footers.Primary.AddParagraph();
            footer.AddText("TravelMate • ");
            footer.AddText(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
            footer.AddText(" • " + loginUzytkownika);
            footer.Format.Font.Size = 8;
            footer.Format.Alignment = ParagraphAlignment.Right;

           
            var podroz = OgolneInfoGrid.DataContext;
            if (podroz != null)
            {
                var title = section.AddParagraph(((dynamic)podroz).Tytul);
                title.Format.Font.Size = 16;
                title.Format.Font.Bold = true;
                title.Format.SpaceAfter = "0.3cm";

                var p1 = section.AddParagraph();
                p1.AddFormattedText("Miasto: ", TextFormat.Bold);
                p1.AddText(((dynamic)podroz).Miasto);

                var p2 = section.AddParagraph();
                p2.AddFormattedText("Kraj: ", TextFormat.Bold);
                p2.AddText(((dynamic)podroz).Kraj);

                var p3 = section.AddParagraph();
                p3.AddFormattedText("Data od – do: ", TextFormat.Bold);
                p3.AddText($"{((dynamic)podroz).DataOd:d} – {((dynamic)podroz).DataDo:d}");

                var p4 = section.AddParagraph();
                p4.AddFormattedText("Opis: ", TextFormat.Bold);
                p4.AddText(((dynamic)podroz).Opis);

                section.AddParagraph("\n");
            }

           
            var statsTitle = section.AddParagraph("Statystyki podróży");
            statsTitle.Format.Font.Bold = true;
            statsTitle.Format.Font.Size = 13;
            statsTitle.Format.SpaceAfter = "0.3cm";


            Table tStats = new Table();
            for (int i = 0; i < 4; i++) tStats.AddColumn(Unit.FromCentimeter(4));

            void AddStatCell(Cell cell, string label, string value)
            {
                if (label == null || value == null) {  return; }
                var p = cell.AddParagraph();
                p.AddFormattedText(label + ": ", TextFormat.Bold);
                p.AddText(value);
                cell.Format.Alignment = ParagraphAlignment.Center;
            }

            var row1 = tStats.AddRow();
            row1.Shading.Color = Colors.AliceBlue;
            AddStatCell(row1.Cells[0], "Łącznie wydane", stats.KosztCalkowity.ToString("N2") + " PLN");
            AddStatCell(row1.Cells[1], "Etapy dojazdów", stats.EtapyDojazdow.ToString());
            AddStatCell(row1.Cells[2], "Noclegi", stats.LiczbaNoclegow.ToString());
            AddStatCell(row1.Cells[3], "Liczba nocy", stats.LiczbaNocy.ToString());

            var row2 = tStats.AddRow();
            AddStatCell(row2.Cells[0], "Liczba punktów zwiedzania", stats.LiczbaPunktow.ToString());
            AddStatCell(row2.Cells[1], "Najlepiej ocenione", stats.NajlepiejOcenione);
            AddStatCell(row2.Cells[2], "Najgorzej ocenione", stats.NajgorzejOcenione);
            AddStatCell(row2.Cells[3], "Koszt noclegów średnio", stats.NoclegiAvg.ToString("N2") + " PLN");

            var row3 = tStats.AddRow();
            AddStatCell(row3.Cells[0], "Czas trwania", stats.CzasTrwaniaDni + " dni");
            AddStatCell(row3.Cells[1], "Ocena", stats.OcenaLiczbowa.HasValue ? $"{stats.OcenaLiczbowa}/5" : "brak");
            AddStatCell(row3.Cells[2], "Najdłuższy etap", stats.NajdluzszyEtapKm + " km");
            AddStatCell(row3.Cells[3], "Najkrótszy etap", stats.NajkrotszyEtapKm + " km");

            StyleTable(tStats);
            section.Add(tStats);
            section.AddParagraph("\n");

            var zwiedzanieTitle = section.AddParagraph("Punkty zwiedzania");
            zwiedzanieTitle.Format.Font.Size = 13;
            zwiedzanieTitle.Format.Font.Bold = true;
            zwiedzanieTitle.Format.SpaceAfter = "0.3cm";
            var tZw = new Table();
            tZw.AddColumn(Unit.FromCentimeter(4));
            tZw.AddColumn(Unit.FromCentimeter(2.5));
            tZw.AddColumn(Unit.FromCentimeter(2.5));
            tZw.AddColumn(Unit.FromCentimeter(1.5));
            tZw.AddColumn(Unit.FromCentimeter(2));
            tZw.AddColumn(Unit.FromCentimeter(3.5));

            var headerZw = tZw.AddRow();
            headerZw.Shading.Color = Colors.LightGray;
            void AddHeaderCell(Cell cell, string text)
            {
                var p = cell.AddParagraph();
                p.AddFormattedText(text, TextFormat.Bold);
                cell.Format.Alignment = ParagraphAlignment.Center;
            }

            AddHeaderCell(headerZw.Cells[0], "Miejsce");
            AddHeaderCell(headerZw.Cells[1], "Typ/Kategoria");
            AddHeaderCell(headerZw.Cells[2], "Data");
            AddHeaderCell(headerZw.Cells[3], "Ocena");
            AddHeaderCell(headerZw.Cells[4], "Cena");
            AddHeaderCell(headerZw.Cells[5], "Uwagi");

            foreach (dynamic i in icZwiedzanie.Items)
            {
                var r = tZw.AddRow();
                r.Cells[0].AddParagraph(i.nazwa);
                r.Cells[1].AddParagraph(i.typ);
                r.Cells[2].AddParagraph(i.data);
                r.Cells[3].AddParagraph(i.ocena.ToString());
                r.Cells[4].AddParagraph(i.cena);
                r.Cells[5].AddParagraph(i.uwagi ?? "");
            }

            StyleTable(tZw);
            section.Add(tZw);
            section.AddParagraph("\n");
            var noclegiTitle = section.AddParagraph("Noclegi");
            noclegiTitle.Format.Font.Size = 13;
            noclegiTitle.Format.Font.Bold = true;
            noclegiTitle.Format.SpaceAfter = "0.3cm";

            var tNo = new Table();
            tNo.AddColumn(Unit.FromCentimeter(4));
            tNo.AddColumn(Unit.FromCentimeter(4));
            tNo.AddColumn(Unit.FromCentimeter(2.5));
            tNo.AddColumn(Unit.FromCentimeter(2.5));
            tNo.AddColumn(Unit.FromCentimeter(2));
            tNo.AddColumn(Unit.FromCentimeter(3));

            var hNo = tNo.AddRow();
            hNo.Shading.Color = Colors.LightGray;
            AddHeaderCell(hNo.Cells[0], "Nazwa");
            AddHeaderCell(hNo.Cells[1], "Adres");
            AddHeaderCell(hNo.Cells[2], "Od");
            AddHeaderCell(hNo.Cells[3], "Do");
            AddHeaderCell(hNo.Cells[4], "Ocena");
            AddHeaderCell(hNo.Cells[5], "Cena");

            foreach (dynamic n in icNoclegi.Items)
            {
                var r = tNo.AddRow();
                r.Cells[0].AddParagraph(n.nazwa);
                r.Cells[1].AddParagraph(n.adres);
                r.Cells[2].AddParagraph(n.data_od);
                r.Cells[3].AddParagraph(n.data_do);
                r.Cells[4].AddParagraph(n.ocena.ToString());
                r.Cells[5].AddParagraph(n.cena);
            }

            StyleTable(tNo);
            section.Add(tNo);
            section.AddParagraph("\n");
            var dojazdyTitle = section.AddParagraph("Dojazdy");
            dojazdyTitle.Format.Font.Size = 13;
            dojazdyTitle.Format.Font.Bold = true;
            dojazdyTitle.Format.SpaceAfter = "0.3cm";

            var tDj = new Table();
            tDj.AddColumn(Unit.FromCentimeter(3));
            tDj.AddColumn(Unit.FromCentimeter(3));
            tDj.AddColumn(Unit.FromCentimeter(2.5));
            tDj.AddColumn(Unit.FromCentimeter(3));
            tDj.AddColumn(Unit.FromCentimeter(3));
            tDj.AddColumn(Unit.FromCentimeter(2));

            var hDj = tDj.AddRow();
            hDj.Shading.Color = Colors.LightGray;
            AddHeaderCell(hDj.Cells[0], "Start");
            AddHeaderCell(hDj.Cells[1], "Cel");
            AddHeaderCell(hDj.Cells[2], "Transport");
            AddHeaderCell(hDj.Cells[3], "Wyjazd");
            AddHeaderCell(hDj.Cells[4], "Przyjazd");
            AddHeaderCell(hDj.Cells[5], "Cena");

            foreach (dynamic d in icDojazdy.Items)
            {
                var r = tDj.AddRow();
                r.Cells[0].AddParagraph(d.start);
                r.Cells[1].AddParagraph(d.cel);
                r.Cells[2].AddParagraph(d.transport);
                r.Cells[3].AddParagraph(d.data_wyjazdu);
                r.Cells[4].AddParagraph(d.data_przyjazdu);
                r.Cells[5].AddParagraph(d.cena);
            }

            StyleTable(tDj);
            section.Add(tDj);


            string path = Path.Combine(Environment.CurrentDirectory, $"RaportWycieczki_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            PdfDocumentRenderer renderer = new PdfDocumentRenderer(true) { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }




        private double ObliczOdleglosc(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; 

            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;

            lat1 *= Math.PI / 180;
            lat2 *= Math.PI / 180;

            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) *
                Math.Cos(lat1) * Math.Cos(lat2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }


        private StatystykiPodrozy PobierzStatystykiPodrozy(int idPodrozy)
        {
            var s = new StatystykiPodrozy();

            using (var conn = new SQLiteConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

       
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT ocena, data_od, data_do FROM podroze WHERE id_podrozy=@id";
                    cmd.Parameters.AddWithValue("@id", idPodrozy);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            if (r["ocena"] != DBNull.Value)
                            {
                                int ocena = Convert.ToInt32(r["ocena"]);
                                s.OcenaLiczbowa = ocena;
                                s.OcenaGwiazdkowa = new string('★', ocena) + new string('☆', 5 - ocena); 
                            }
                            else
                            {
                                s.OcenaLiczbowa = null;
                                s.OcenaGwiazdkowa = "brak oceny";
                            }


            
                            DateTime od = DateTime.Parse(r["data_od"].ToString());
                            DateTime doDnia = DateTime.Parse(r["data_do"].ToString());
                            s.CzasTrwaniaDni = (doDnia - od).Days + 1;
                        }
                    }
                }





                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT cena, waluta FROM dojazdy WHERE id_podrozy=@id";
                    cmd.Parameters.AddWithValue("@id", idPodrozy);

                    var ceny = new List<double>();

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            double cenaPLN = ConvertToPLN(r["cena"] != DBNull.Value ? Convert.ToDouble(r["cena"]) : 0,
                                                          r["waluta"].ToString());
                            ceny.Add(cenaPLN);
                        }
                    }

                    if (ceny.Count > 0)
                    {
                        s.DojazdyMin = ceny.Min();
                        s.DojazdyMax = ceny.Max();
                        s.DojazdyAvg = ceny.Average();
                        s.DojazdySum = ceny.Sum();
                        s.EtapyDojazdow = ceny.Count;
                    }




                    using (var cmdKm = conn.CreateCommand())
                    {
                        cmdKm.CommandText = @"
        SELECT ps.szerokosc AS latStart, ps.dlugosc AS lonStart,
               pk.szerokosc AS latEnd, pk.dlugosc AS lonEnd
        FROM dojazdy d
        JOIN punkty_dojazdow ps ON d.id_punktu_start = ps.id_punktu
        JOIN punkty_dojazdow pk ON d.id_punktu_koniec = pk.id_punktu
        WHERE d.id_podrozy = @id
    ";
                        cmdKm.Parameters.AddWithValue("@id", idPodrozy);

                        var odleglosci = new List<double>();

                        using (var r = cmdKm.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                double latStart = Convert.ToDouble(r["latStart"]);
                                double lonStart = Convert.ToDouble(r["lonStart"]);
                                double latEnd = Convert.ToDouble(r["latEnd"]);
                                double lonEnd = Convert.ToDouble(r["lonEnd"]);

                                double km = ObliczOdleglosc(latStart, lonStart, latEnd, lonEnd);
                                odleglosci.Add(km);
                            }
                        }

                        if (odleglosci.Count > 0)
                        {
                            s.NajdluzszyEtapKm = odleglosci.Max();
                            s.NajkrotszyEtapKm = odleglosci.Min();
                        }
                    }


                }


                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT cena, waluta, julianday(data_do) - julianday(data_od) AS nocy FROM noclegi WHERE id_podrozy=@id";
                    cmd.Parameters.AddWithValue("@id", idPodrozy);

                    var ceny = new List<double>();

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            double cenaPLN = ConvertToPLN(r["cena"] != DBNull.Value ? Convert.ToDouble(r["cena"]) : 0,
                                                          r["waluta"].ToString());
                            ceny.Add(cenaPLN);
                            s.LiczbaNocy += r["nocy"] != DBNull.Value ? Convert.ToInt32(r["nocy"]) : 0;
                        }
                    }

                    if (ceny.Count > 0)
                    {
                        s.NoclegiMin = ceny.Min();
                        s.NoclegiMax = ceny.Max();
                        s.NoclegiAvg = ceny.Average();
                        s.LiczbaNoclegow = ceny.Count;
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT z.cena, z.waluta, z.ocena, p.nazwa FROM zwiedzanie z JOIN punkty_zwiedzania p ON z.id_punktu = p.id_punktu WHERE z.id_podrozy=@id";
                    cmd.Parameters.AddWithValue("@id", idPodrozy);

                    var ceny = new List<double>();
                    var oceny = new List<(int ocena, string nazwa)>();

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            double cenaPLN = ConvertToPLN(r["cena"] != DBNull.Value ? Convert.ToDouble(r["cena"]) : 0,
                                                          r["waluta"].ToString());
                            ceny.Add(cenaPLN);

                            if (r["ocena"] != DBNull.Value)
                                oceny.Add((Convert.ToInt32(r["ocena"]), r["nazwa"].ToString()));
                        }
                    }

                    if (ceny.Count > 0)
                    {
                        s.ZwiedzanieMin = ceny.Min();
                        s.ZwiedzanieMax = ceny.Max();
                        s.ZwiedzanieSum = ceny.Sum();
                        s.LiczbaPunktow = ceny.Count;
                    }

                    if (oceny.Count > 0)
                    {
                        s.NajlepiejOcenione = oceny.OrderByDescending(o => o.ocena).First().nazwa;
                        s.NajgorzejOcenione = oceny.OrderBy(o => o.ocena).First().nazwa;
                    }
                }


                s.KosztCalkowity = s.DojazdySum + s.ZwiedzanieSum + (s.NoclegiAvg * s.LiczbaNocy);



            }

            return s;
        }



        private Dictionary<string, double> rates = new Dictionary<string, double>();

        public async Task LoadRatesAsync()
        {
            rates = await GetExchangeRatesAsync("PLN");
        }

        public async Task<Dictionary<string, double>> GetExchangeRatesAsync(string baseCurrency)
        {
            using (var client = new HttpClient())
            {
                string url = $"https://api.frankfurter.app/latest?from={baseCurrency}";
                var json = await client.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<FrankfurterResponse>(json);

                if (!data.rates.ContainsKey(baseCurrency))
                    data.rates[baseCurrency] = 1;

                return data.rates;
            }
        }

        public double ConvertToPLN(double amount, string fromCurrency)
        {
            if (fromCurrency == "PLN") return amount;
            double rate = rates.ContainsKey(fromCurrency) ? rates[fromCurrency] : 1;
            return Math.Round(amount / rate, 2);
        }

        public class FrankfurterResponse
        {
            public string @base { get; set; }
            public Dictionary<string, double> rates { get; set; }
        }

        private void BtnDrukuj_Click(object sender, RoutedEventArgs e)
        {
            BtnPDF_Click(sender, e);
        }
    }

    public class StatystykiPodrozy
    {

        public double KosztCalkowity { get; set; }
        public int EtapyDojazdow { get; set; }
        public int LiczbaNoclegow { get; set; }
        public int LiczbaNocy { get; set; }
        public int LiczbaPunktow { get; set; }
        public int CzasTrwaniaDni { get; set; }
        public string OcenaGwiazdkowa { get; set; }
        public int? OcenaLiczbowa { get; set; }


        public double DojazdyMin { get; set; }
        public double DojazdyMax { get; set; }
        public double DojazdyAvg { get; set; }
        public double DojazdySum { get; set; }
        public double NajdluzszyEtapKm { get; set; }
        public double NajkrotszyEtapKm { get; set; }


        public double NoclegiMin { get; set; }
        public double NoclegiMax { get; set; }
        public double NoclegiAvg { get; set; }


        public double ZwiedzanieMin { get; set; }
        public double ZwiedzanieMax { get; set; }
        public double ZwiedzanieSum { get; set; }
        public string NajlepiejOcenione { get; set; }
        public string NajgorzejOcenione { get; set; }
    }

}
