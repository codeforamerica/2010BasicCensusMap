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


namespace BasicCensusMap
{
    public partial class MainPage : UserControl
    {
        Locator _locatorTask;
        GraphicsLayer _candidateGraphicsLayer;
        private static ESRI.ArcGIS.Client.Projection.WebMercator _mercator =
          new ESRI.ArcGIS.Client.Projection.WebMercator();


        public MainPage()
        {
            InitializeComponent();

            //ESRI.ArcGIS.Client.Geometry.Envelope initialExtent =
            // new ESRI.ArcGIS.Client.Geometry.Envelope(-13205480.536, 4077189.785, -13176602.592, 4090421.641);

            //MyMap.Extent = initialExtent;

            _candidateGraphicsLayer = MyMap.Layers["CandidateGraphicsLayer"] as GraphicsLayer;
        }

        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            

            ArcGISTiledMapServiceLayer arcgisLayer = MyMap.Layers["CensusLayer"] as ArcGISTiledMapServiceLayer;
            arcgisLayer.Url = ((RadioButton)sender).Tag as string;
            //arcgisLayer.Opacity = 0.5;
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

        private void MyMap_MouseClick(object sender, ESRI.ArcGIS.Client.Map.MouseEventArgs e)
        {
            FeatureLayer featureLayer = MyMap.Layers["MyFeatureLayer"] as FeatureLayer;
            System.Windows.Point screenPnt = MyMap.MapToScreen(e.MapPoint);

            // Account for difference between Map and application origin
            GeneralTransform generalTransform = MyMap.TransformToVisual(Application.Current.RootVisual);
            System.Windows.Point transformScreenPnt = generalTransform.Transform(screenPnt);

            IEnumerable<Graphic> selected =
                featureLayer.FindGraphicsInHostCoordinates(transformScreenPnt);

            foreach (Graphic g in selected)
            {

                MyInfoWindow.Anchor = e.MapPoint;
                MyInfoWindow.IsOpen = true;
                //Since a ContentTemplate is defined, Content will define the DataContext for the ContentTemplate
                MyInfoWindow.Content = g.Attributes;
                return;
            }

            InfoWindow window = new InfoWindow()
            {
                Anchor = e.MapPoint,
                Map = MyMap,
                IsOpen = true,
                ContentTemplate = LayoutRoot.Resources["LocationInfoWindowTemplate"] as System.Windows.DataTemplate,
                //Since a ContentTemplate is defined, Content will define the DataContext for the ContentTemplate
                Content = e.MapPoint
            };
            LayoutRoot.Children.Add(window);
        }
        

        private void MyInfoWindow_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MyInfoWindow.IsOpen = false;
        }



    }


    }

