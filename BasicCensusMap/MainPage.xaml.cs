using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Toolkit;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Tasks;
using System.Json;

namespace BasicCensusMap
{
    public partial class MainPage : UserControl
    {
        Locator _locatorTask;
        GraphicsLayer _candidateGraphicsLayer;
        private static ESRI.ArcGIS.Client.Projection.WebMercator _mercator =
          new ESRI.ArcGIS.Client.Projection.WebMercator();

        private List<DataItem> _dataItems = null;

        private ArcGISTiledMapServiceLayer arcgisLayer;
         
        public MainPage()
        {
            InitializeComponent();

            //ESRI.ArcGIS.Client.Geometry.Envelope initialExtent =
            // new ESRI.ArcGIS.Client.Geometry.Envelope(-13205480.536, 4077189.785, -13176602.592, 4090421.641);

            //MyMap.Extent = initialExtent;

            _candidateGraphicsLayer = MyMap.Layers["CandidateGraphicsLayer"] as GraphicsLayer;
            arcgisLayer = MyMap.Layers["CensusLayer"] as ArcGISTiledMapServiceLayer;

            LoadComboBoxData();
           

           


        }

      

        private void BaseMapRadioButton_Click(object sender, RoutedEventArgs e)
        {
            ArcGISTiledMapServiceLayer arcgisLayer = MyMap.Layers["BaseLayer"] as ArcGISTiledMapServiceLayer;
            arcgisLayer.Url = ((RadioButton)sender).Tag as string;

        }

      private void FindAddressButton_Click(object sender, RoutedEventArgs e)
        {
            _locatorTask = new Locator("http://tasks.arcgisonline.com/ArcGIS/rest/services/Locators/TA_Address_NA/GeocodeServer");
            _locatorTask.AddressToLocationsCompleted += LocatorTask_AddressToLocationsCompleted;
            _locatorTask.Failed += LocatorTask_Failed;

            AddressToLocationsParameters addressParams = new AddressToLocationsParameters()
            {
                OutSpatialReference = MyMap.SpatialReference
            };

            Dictionary<string, string> address = addressParams.Address;

           
            if (!string.IsNullOrEmpty(City.Text))
                address.Add("City", City.Text);
            if (!string.IsNullOrEmpty(State.Text))
                address.Add("State", State.Text);
          

            _locatorTask.AddressToLocationsAsync(addressParams);
        }

        private void LocatorTask_AddressToLocationsCompleted(object sender, ESRI.ArcGIS.Client.Tasks.AddressToLocationsEventArgs args)
        {
            _candidateGraphicsLayer.ClearGraphics();
            CandidateListBox.Items.Clear();

            List<AddressCandidate> returnedCandidates = args.Results;

            foreach (AddressCandidate candidate in returnedCandidates)
            {
                if (candidate.Score >= 80)
                {
                    CandidateListBox.Items.Add(candidate.Address);

                    Graphic graphic = new Graphic()
                    {
                        Symbol = LayoutRoot.Resources["DefaultMarkerSymbol"] as ESRI.ArcGIS.Client.Symbols.Symbol,
                        Geometry = candidate.Location
                    };

                    graphic.Attributes.Add("Address", candidate.Address);

                    string latlon = String.Format("{0}, {1}", candidate.Location.X, candidate.Location.Y);
                    graphic.Attributes.Add("LatLon", latlon);

                    if (candidate.Location.SpatialReference == null)
                    {
                        candidate.Location.SpatialReference = new SpatialReference(4326);
                    }

                    if (!candidate.Location.SpatialReference.Equals(MyMap.SpatialReference))
                    {
                        if (MyMap.SpatialReference.Equals(new SpatialReference(102100)) && candidate.Location.SpatialReference.Equals(new SpatialReference(4326)))
                            graphic.Geometry = _mercator.FromGeographic(graphic.Geometry);
                        else if (MyMap.SpatialReference.Equals(new SpatialReference(4326)) && candidate.Location.SpatialReference.Equals(new SpatialReference(102100)))
                            graphic.Geometry = _mercator.ToGeographic(graphic.Geometry);
                        else if (MyMap.SpatialReference != new SpatialReference(4326))
                        {
                            GeometryService geometryService =
                                new GeometryService("http://sampleserver3.arcgisonline.com/ArcGIS/rest/services/Geometry/GeometryServer");

                            geometryService.ProjectCompleted += (s, a) =>
                            {
                                graphic.Geometry = a.Results[0].Geometry;
                            };

                            geometryService.Failed += (s, a) =>
                            {
                                MessageBox.Show("Projection error: " + a.Error.Message);
                            };

                            geometryService.ProjectAsync(new List<Graphic> { graphic }, MyMap.SpatialReference);
                        }
                    }

                    _candidateGraphicsLayer.Graphics.Add(graphic);
                }
            }

            if (_candidateGraphicsLayer.Graphics.Count > 0)
            {
                CandidatePanelGrid.Visibility = Visibility.Visible;
                CandidateListBox.SelectedIndex = 0;
            }
        }

        void _candidateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = (sender as ListBox).SelectedIndex;
            if (index >= 0)
            {
                MapPoint candidatePoint = _candidateGraphicsLayer.Graphics[index].Geometry as MapPoint;
                double displaySize = MyMap.MinimumResolution * 30000;

                ESRI.ArcGIS.Client.Geometry.Envelope displayExtent = new ESRI.ArcGIS.Client.Geometry.Envelope(
                    candidatePoint.X - (displaySize / 2),
                    candidatePoint.Y - (displaySize / 2),
                    candidatePoint.X + (displaySize / 2),
                    candidatePoint.Y + (displaySize / 2));

                MyMap.ZoomTo(displayExtent);
            }
        }

        private void LocatorTask_Failed(object sender, TaskFailedEventArgs e)
        {
            MessageBox.Show("Locator service failed: " + e.Error);
        }

        private void QueryPoint_MouseClick(object sender, ESRI.ArcGIS.Client.Map.MouseEventArgs e)
        {
            ESRI.ArcGIS.Client.Geometry.MapPoint clickPoint = e.MapPoint;

            ESRI.ArcGIS.Client.Tasks.IdentifyParameters identifyParams = new IdentifyParameters()
            {
                Geometry = clickPoint,
                MapExtent = MyMap.Extent,
                Width = (int)MyMap.ActualWidth,
                Height = (int)MyMap.ActualHeight,
                LayerOption = LayerOption.visible,
                SpatialReference = MyMap.SpatialReference
            };

          

            //IdentifyTask identifyTask = new IdentifyTask("http://server.arcgisonline.com/ArcGIS/rest/services/Demographics/USA_Average_Household_Size/MapServer/");
            IdentifyTask identifyTask = new IdentifyTask(arcgisLayer.Url);
            
            
            identifyTask.ExecuteCompleted += IdentifyTask_ExecuteCompleted;
            identifyTask.Failed += IdentifyTask_Failed;
            identifyTask.ExecuteAsync(identifyParams);

            GraphicsLayer graphicsLayer = MyMap.Layers["MyGraphicsLayer"] as GraphicsLayer;
            graphicsLayer.ClearGraphics();
            ESRI.ArcGIS.Client.Graphic graphic = new ESRI.ArcGIS.Client.Graphic()
            {
                Geometry = clickPoint,
                Symbol = LayoutRoot.Resources["DefaultPictureSymbol"] as ESRI.ArcGIS.Client.Symbols.Symbol
            };
            graphicsLayer.Graphics.Add(graphic);
        }

        public void ShowFeatures(List<IdentifyResult> results)
        {
            _dataItems = new List<DataItem>();

            if (results != null && results.Count > 0)
            {
                IdentifyComboBox.Items.Clear();
                foreach (IdentifyResult result in results)
                {
                    Graphic feature = result.Feature;
                    string title = result.Value.ToString() + " (" + result.LayerName + ")";
                    _dataItems.Add(new DataItem()
                    {
                        Title = title,
                        Data = feature.Attributes
                    });
                    IdentifyComboBox.Items.Add(title);
                }
                IdentifyComboBox.SelectedIndex = 0;
            }
        }

        void cb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = IdentifyComboBox.SelectedIndex;
            if (index > -1)
                IdentifyDetailsDataGrid.ItemsSource = _dataItems[index].Data;
        }

        private void IdentifyTask_ExecuteCompleted(object sender, IdentifyEventArgs args)
        {
            IdentifyDetailsDataGrid.ItemsSource = null;

            if (args.IdentifyResults != null && args.IdentifyResults.Count > 0)
            {
                IdentifyResultsPanel.Visibility = Visibility.Visible;

                ShowFeatures(args.IdentifyResults);
            }
            else
            {
                IdentifyComboBox.Items.Clear();
                IdentifyComboBox.UpdateLayout();

                IdentifyResultsPanel.Visibility = Visibility.Collapsed;
            }
        }

        public class DataItem
        {
            public string Title { get; set; }
            public IDictionary<string, object> Data { get; set; }
        }

        void IdentifyTask_Failed(object sender, TaskFailedEventArgs e)
        {
            MessageBox.Show("Identify failed. Error: " + e.Error);
        }

        //Combobox layer code
        
        private void LoadComboBoxData()
        {
            //Uri serviceUri = new Uri("http://server.arcgisonline.com/ArcGIS/rest/services/Demographics?f=json", UriKind.Absolute);
            //WebClient downloader = new WebClient();
            //downloader.OpenReadCompleted += new OpenReadCompletedEventHandler(downloader_OpenReadCompleted);
            //downloader.OpenReadAsync(serviceUri);

            string[] strArray = {
            "Average_Household_Size",
            "Daytime_Population",
            "Diversity_Index",
            "Labor_Force_Participation_Rate", 
            "Median_Home_Value",
            "Median_Household_Income",
            "Median_Net_Worth",
            "Owner_Occupied_Housing",
            "Percent_Male",
            "Percent_Over_64",
            "Percent_Under_18",
            "Population_by_Sex",
            "Population_Density",
            "Projected_Population_Change",
            "Recent_Population_Change",
            "Retail_Spending_Potential",
            "Tapestry",
            "Unemployment_Rate" };
           
            
              ComboBoxLayer.ItemsSource = strArray;


        }

        void cbLayer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
          
            //http://server.arcgisonline.com/ArcGIS/rest/services/Demographics/USA_Population_Density/MapServer

            string BaseUri = "http://server.arcgisonline.com/ArcGIS/rest/services/Demographics/USA_";
            string currentLayer = (string)ComboBoxLayer.SelectedValue;


            arcgisLayer.Url = BaseUri + currentLayer + "/MapServer";
          
        }


        void downloader_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e)
        {
           
            //JsonArray jsonArray = (JsonArray)JsonArray.Load(e.Result);


            //JsonObject jsonArray = (JsonObject)JsonObject.Load(e.Result);

            //var query = from layer in jsonArray
            //            select new CensusLayer
            //            {
            //                //LayerName = (string)layer["name"],
            //                //MapType = (string)layer["type"]

            //            };


            //List<CensusLayer> layers = query.ToList() as List<CensusLayer>;

            //ComboBox1.ItemsSource = layers;

         


          
        }


    }


    }

