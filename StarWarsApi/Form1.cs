using StarWarsApi.Core;

namespace StarWarsApi
{
    public partial class Form1 : Form
    {
        private readonly SwapiClient _swapi = new();
        private bool _loading;

        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            await LoadStarshipsAsync().ConfigureAwait(true);
        }

        private async void btnLoad_Click(object sender, EventArgs e)
        {
            await LoadStarshipsAsync().ConfigureAwait(true);
        }

        private async Task LoadStarshipsAsync()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;
            btnLoad.Enabled = false;
            lblStatus.Text = "Loading starships from SWAPI…";
            listStarships.Items.Clear();
            txtOutput.Clear();

            try
            {
                var ships = await _swapi.GetStarshipsAtLeastLengthAsync(minLength: 10)
                    .ConfigureAwait(true);

                foreach (var ship in ships)
                {
                    var line = StarshipPrinter.Format(ship);
                    txtOutput.AppendText(line + Environment.NewLine);

                    var item = new ListViewItem(ship.Name);
                    item.SubItems.Add(ship.LengthRaw);
                    item.SubItems.Add(StarshipPrinter.FormatPilots(ship));
                    listStarships.Items.Add(item);
                }

                lblStatus.Text = $"Showing {ships.Count} starship(s) with length ≥ 10.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to load starships.";
                MessageBox.Show(
                    this,
                    ex.Message,
                    "SWAPI error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnLoad.Enabled = true;
                _loading = false;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _swapi.Dispose();
            base.OnFormClosed(e);
        }
    }
}
