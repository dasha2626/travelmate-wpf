using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace TravelMate
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {



        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DatabaseConfig.EnsureDatabase();

            this.ShutdownMode = ShutdownMode.OnLastWindowClose;
        }


    }
}
