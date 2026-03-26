using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static TravelMate.MapWindow;

namespace TravelMate
{
    public static class PdfExporter
    {
        public static void EksportujPodroze(
            string sciezkaPliku,
            List<Podroz> podroze,
            DateTime? od,
            DateTime? do_,
            string sortowanie,
            string lacznieWydane,
            string lacznieNocy,
            string lacznieLotow,
            string laczniePociagow,
            string sredniKoszt,
            string lacznieDni,
            string lacznieKm,
            string najdrozsza,
            string najtansza,
            string kraje,
            string miasta,
            string sredniaOcena,
            string topKategoria,
            string loginUzytkownika
        )
        {
            var doc = new Document();
            doc.Info.Title = "Raport podróży – TravelMate";
            var sec = doc.AddSection();
            sec.PageSetup.TopMargin = Unit.FromCentimeter(2);
            sec.PageSetup.BottomMargin = Unit.FromCentimeter(2);

          
            var footer = sec.Footers.Primary.AddParagraph();
            footer.AddText("TravelMate • ");
            footer.AddText(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
            footer.AddText(" • " + loginUzytkownika);
            footer.Format.Font.Size = 8;
            footer.Format.Alignment = ParagraphAlignment.Right;

       
            var title = sec.AddParagraph("Raport podróży");
            title.Format.Font.Size = 16;
            title.Format.Font.Bold = true;
            title.Format.SpaceAfter = "0.4cm";

          
            var pFiltry = sec.AddParagraph();
            pFiltry.AddFormattedText("Filtry: ", TextFormat.Bold);
            pFiltry.AddText($"Od: {(od.HasValue ? od.Value.ToString("dd.MM.yyyy") : "-")}   ");
            pFiltry.AddText($"Do: {(do_.HasValue ? do_.Value.ToString("dd.MM.yyyy") : "-")}   ");

            pFiltry.AddText($"Sortowanie: {sortowanie}");
            pFiltry.Format.SpaceAfter = "0.6cm";

         
            var statsTitle = sec.AddParagraph("Statystyki");
            statsTitle.Format.Font.Size = 13;
            statsTitle.Format.Font.Bold = true;
            statsTitle.Format.SpaceAfter = "0.3cm";

            Table tableStats = sec.AddTable();
            for (int i = 0; i < 4; i++)
                tableStats.AddColumn(Unit.FromCentimeter(4));

            var r1 = tableStats.AddRow();
            r1.Shading.Color = Colors.AliceBlue;
            AddStat(r1.Cells[0], "Łącznie wydane", lacznieWydane);
            AddStat(r1.Cells[1], "Łącznie nocy", lacznieNocy);
            AddStat(r1.Cells[2], "Łącznie lotów", lacznieLotow);
            AddStat(r1.Cells[3], "Łącznie pociągów", laczniePociagow);

            var r2 = tableStats.AddRow();
            AddStat(r2.Cells[0], "Średni koszt", sredniKoszt);
            AddStat(r2.Cells[1], "Łącznie km", lacznieKm);
            AddStat(r2.Cells[2], "Najdroższa", najdrozsza);
            AddStat(r2.Cells[3], "Najtańsza", najtansza);

            var r3 = tableStats.AddRow();
            r3.Shading.Color = Colors.AliceBlue;
            AddStat(r3.Cells[0], "Odwiedzone kraje", kraje);
            AddStat(r3.Cells[1], "Odwiedzone miasta", miasta);
            AddStat(r3.Cells[2], "Średnia ocena", sredniaOcena);
            AddStat(r3.Cells[3], "Top kategoria", topKategoria);

            StyleTable(tableStats, centerText: true);

          
            var listTitle = sec.AddParagraph("Lista podróży");
            listTitle.Format.Font.Size = 13;
            listTitle.Format.Font.Bold = true;
            listTitle.Format.SpaceAfter = "0.4cm";

            Table table = sec.AddTable();
            table.AddColumn(Unit.FromCentimeter(1)); 
            table.AddColumn(Unit.FromCentimeter(4)); 
            table.AddColumn(Unit.FromCentimeter(6)); 
            table.AddColumn(Unit.FromCentimeter(3)); 
            table.AddColumn(Unit.FromCentimeter(3)); 

            var header = table.AddRow();
            header.Shading.Color = Colors.LightGray;
            header.Format.Font.Bold = true;
            header.Format.Alignment = ParagraphAlignment.Center;
            header.Cells[0].AddParagraph("Nr");
            header.Cells[1].AddParagraph("Nazwa");
            header.Cells[2].AddParagraph("Adres");
            header.Cells[3].AddParagraph("Data od");
            header.Cells[4].AddParagraph("Data do");

            int iNr = 1;
            foreach (var p in podroze)
            {
                var row = table.AddRow();
                row.Cells[0].AddParagraph(iNr++.ToString());
                row.Cells[1].AddParagraph(p.tytul);
                row.Cells[2].AddParagraph(p.pelnyAdres);
                row.Cells[3].AddParagraph(p.data_od.ToString("dd.MM.yyyy"));
                row.Cells[4].AddParagraph(p.data_do.ToString("dd.MM.yyyy"));
            }

            StyleTable(table, centerText: false);

          
            var renderer = new PdfDocumentRenderer(true) { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(sciezkaPliku);
            Process.Start(new ProcessStartInfo(sciezkaPliku) { UseShellExecute = true });
        }

        private static void AddStat(Cell cell, string label, string value)
        {
            var p = cell.AddParagraph();
            p.AddFormattedText(label + ": ", TextFormat.Bold);
            p.AddText(value);
            cell.Format.Alignment = ParagraphAlignment.Center;
        }

        private static void StyleTable(Table table, bool centerText)
        {
            table.Borders.Width = 0.5;
            table.Format.Alignment = ParagraphAlignment.Center;

            foreach (Row row in table.Rows)
            {
                row.TopPadding = Unit.FromMillimeter(2);
                row.BottomPadding = Unit.FromMillimeter(2);

                foreach (Cell cell in row.Cells)
                {
                    cell.VerticalAlignment = VerticalAlignment.Center;
                    cell.Format.Alignment = centerText ? ParagraphAlignment.Center : ParagraphAlignment.Left;
                }
            }
        }
    }
}
