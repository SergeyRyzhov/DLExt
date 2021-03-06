﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using DLExt.Builder;
using DLExt.Builder.Model;
using DLExt.Builder.Retrievers;
using Microsoft.Office.Interop.Outlook;

namespace DLExt.OutlookAddin
{
    public partial class MainWindow : INotifyPropertyChanged
    {
        private readonly BackgroundWorker loadingWorker;
        private readonly BackgroundWorker composeWorker;
        private AddressBuilder builder;
        private IList<Location> locationsList;
        private IList<Person> personsList;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Server { get; private set; }

        public string LocationsRootPath { get; private set; }

        public string PersonsRootPath { get; private set; }

        private bool isProcessing;

        public bool IsProcessing
        {
            get { return isProcessing; }
            set
            {
                isProcessing = value;
                PropertyChanged(this, new PropertyChangedEventArgs("IsProcessing"));
            }
        }

        public MainWindow(string server, string locationsRootPath, string personsRootPath)
        {
            Server = server;
            LocationsRootPath = locationsRootPath;
            PersonsRootPath = personsRootPath;

            loadingWorker = new BackgroundWorker();
            loadingWorker.DoWork += (o, args) =>
            {
                IsProcessing = true;
                locationsList = new LocationsRetriever(Server).Retrieve(LocationsRootPath);
                personsList = new PersonsRetriever(Server).Retrieve(PersonsRootPath);
            };

            loadingWorker.RunWorkerCompleted += (sender, args) =>
            {
                locations.ItemsSource = locationsList;
                persons.ItemsSource = personsList;
                IsProcessing = false;
            };

            composeWorker = new BackgroundWorker();
            composeWorker.DoWork += (sender, args) =>
            {
                IsProcessing = true;
                builder = new AddressBuilder(
                    Server,
                    locations.Items.OfType<Location>().Where(loc => loc.IsSelected),
                    personsToExclude.Items.OfType<Person>());
                builder.Build();
            };

            composeWorker.RunWorkerCompleted += (sender, args) =>
            {
                IsProcessing = false;
                try
                {
                    var app = new Microsoft.Office.Interop.Outlook.Application();
                    var mailItem = (MailItem)app.CreateItem(OlItemType.olMailItem);

                    mailItem.To = builder.ResultAddress;
                    mailItem.Display(true);

                }
                catch (COMException)
                {
                }
            };

            loadingWorker.WorkerSupportsCancellation = true;
            DataContext = this;
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            loadingWorker.RunWorkerAsync();
        }

        private void ComposeEmail(object sender, RoutedEventArgs e)
        {
            composeWorker.RunWorkerAsync();
        }

        private void ExcludePerson(object sender, RoutedEventArgs e)
        {
            if (!personsToExclude.Items.Contains(persons.SelectedItem))
            {
                personsToExclude.Items.Add(persons.SelectedItem);
            }
        }

        private void CloseForm(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            if (loadingWorker.IsBusy)
            {
                loadingWorker.CancelAsync();
            }
        }
    }
}
