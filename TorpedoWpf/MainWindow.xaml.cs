using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.Json;

namespace TorpedoWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public class Ship
    {
        public int Length { get; set; }
        public List<(int Row, int Col)> Positions { get; set; } = new List<(int, int)>();

        public bool IsRow { get; set; } = false;  // Igaz, ha sorban
        public bool IsCol { get; set; } = false;
    }

    public partial class MainWindow : Window
    {
        private char[,] leftMap = new char[10, 10];
        private char[,] rightMap = new char[10, 10];

        private bool firstSelection = true;
        private int remainingCellsToPlace = 0;
        private bool isPlacingShip = false;
        private bool gameStarted = false;

        private Ship currentShip = null;
        private List<Ship> placedShips = new List<Ship>();

        private ClientWebSocket _webSocket;
        private int _playerNumber = 1;
        private int _currentTurn = 1;

        public MainWindow()
        {
            InitializeComponent();
            InitializeButtons(gPlayerField, leftMap, isLeftSide: true);
            InitializeButtons(gOpponentField, rightMap, isLeftSide: false);
            InitializeWebSocket();
            // ListBox kiválasztásának eseménykezelője
            ShipListBox.SelectionChanged += ShipListBox_SelectionChanged;

            // Ellenőrizzük a gomb láthatóságát az inicializáláskor is
            CheckStartGameButtonVisibility();
        }

        private async void InitializeWebSocket()
        {
            try
            {
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri("ws://localhost:5000/"), CancellationToken.None);
                MessageBox.Show("Connected to server!", "Connection", MessageBoxButton.OK, MessageBoxImage.Information);

                // Start listening for messages
                _ = ListenToServer();

                // Send a ready message to the server
                await SendMessageAsync("READY");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ListenToServer()
        {
            var buffer = new byte[1024];

            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    MessageBox.Show("Disconnected from server.", "Connection", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                HandleServerMessage(message);
            }
        }

        private void HandleServerMessage(string message)
        {
            if (message.StartsWith("PLAYER"))
            {
                // Example: "PLAYER:1"
                _playerNumber = int.Parse(message.Split(':')[1]);
                MessageBox.Show($"You are Player {_playerNumber}.", "Player Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Handle other messages (e.g., moves, game state updates)
                MessageBox.Show($"Server: {message}", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task SendMessageAsync(string message)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }

        private async void SendMove(int row, int col)
        {
            var move = $"{row},{col}";
            await SendMessageAsync(move);
        }



        private void CheckStartGameButtonVisibility()
        {
            // Ha nincs elem a ShipListBox-ban, akkor a gomb megjelenik, különben elrejtjük
            StartGameButton.Visibility = ShipListBox.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        private void InitializeButtons(Grid grid, char[,] map, bool isLeftSide)
        {
            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 10; col++)
                {
                    Button button = new Button
                    {
                        Width = 40,
                        Height = 40,
                        Background = Brushes.LightGray
                    };

                    // Sor és oszlop beállítása
                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, col);

                    // Eseménykezelők hozzárendelése
                    int currentRow = row;
                    int currentCol = col;

                    if (isLeftSide)
                    {
                        button.Click += (sender, e) => Button_Click(sender, e, map, currentRow, currentCol);
                    }
                    else
                    {
                        button.Click += RightButtonGrid_Click;
                    }

                    // Gomb hozzáadása a Gridhez
                    grid.Children.Add(button);
                }
            }
        }

        private void RightButtonGrid_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Ellenfél oldala, ide nem pakolhatsz hajót", "Figyelmeztetés", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ShipListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isPlacingShip)
            {

                MessageBox.Show("Előbb fejezd be az aktuális hajó lerakását, mielőtt másikat választasz!", "Figyelmeztetés", MessageBoxButton.OK, MessageBoxImage.Warning);
                ShipListBox.SelectionChanged -= ShipListBox_SelectionChanged;
                ShipListBox.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
                ShipListBox.SelectionChanged += ShipListBox_SelectionChanged;
            }
            else if (ShipListBox.SelectedItem != null)
            {
                SetShipLength();
                isPlacingShip = true;
            }
        }

        private void SetShipLength()
        {
            var selectedItem = ShipListBox.SelectedItem as ListBoxItem;
            if (selectedItem != null)
            {
                var match = Regex.Match(selectedItem.Content.ToString(), @"(\d+) mező hosszú");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int length))
                {
                    remainingCellsToPlace = length;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e, char[,] map, int row, int col)
        {
            if (gameStarted)
            {
                // If the game has started, prevent ship placement and handle game moves
                MessageBox.Show("A játék elindult, nem változtathatod meg a hajók elhelyezését!", "Figyelmeztetés", MessageBoxButton.OK, MessageBoxImage.Warning);

                if (_playerNumber != _currentTurn)
                {
                    MessageBox.Show("It's not your turn!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if the cell belongs to an existing ship
                var ship = placedShips.Find(s => s.Positions.Contains((row, col)));
                if (ship != null)
                {
                    // Remove ship from the map and update its position visually
                    foreach (var pos in ship.Positions)
                    {
                        map[pos.Row, pos.Col] = '\0'; // Reset the map cell to empty
                        GetButtonFromGrid(gPlayerField, pos.Row, pos.Col).Background = Brushes.LightGray; // Reset button color
                    }

                    // Remove the ship from the placedShips list
                    placedShips.Remove(ship);

                    // Return the ship to the ShipListBox
                    var shipItem = new ListBoxItem { Content = $"{ship.Length} mező hosszú" };
                    ShipListBox.Items.Add(shipItem);

                    // Re-enable the Start Game button if necessary
                    CheckStartGameButtonVisibility();

                    // Update adjacent cells to allow placement again
                    MarkAdjacentCells(map);

                    // Clear all "X" markings to prevent interference
                    ClearAdjacentMarkings(map);
                }
            }
            else if (ShipListBox.SelectedItem != null && map[row, col] != '1' && map[row, col] != 'X')
            {
                // Handle ship placement during setup
                if (firstSelection)
                {
                    PlaceShip(sender, map, row, col);
                    firstSelection = false; // Mark the first selection
                }
                else if (IsAdjacentToSelected(map, row, col))
                {
                    PlaceShip(sender, map, row, col); // Place ship if adjacent
                }
                else
                {
                    MessageBox.Show("Csak szomszédos mezőt jelölhetsz ki!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }


        private void ClearAdjacentMarkings(char[,] map)
        {
            for (int row = 0; row < 10; row++)
            {
                for (int col = 0; col < 10; col++)
                {
                    if (map[row, col] == 'X')
                    {
                        map[row, col] = '\0'; // Eltávolítjuk az X jelet
                        GetButtonFromGrid(gPlayerField, row, col).Background = Brushes.LightGray; // Visszaállítjuk a gombok színét
                    }
                }
            }
        }



        private void PlaceShip(object sender, char[,] map, int row, int col)
        {
            if (currentShip == null)
            {
                currentShip = new Ship { Length = remainingCellsToPlace };
                placedShips.Add(currentShip);
            }

            // Ha már van egy pozíció, rögzítjük az irányt
            if (currentShip.Positions.Count > 0)
            {
                var firstPos = currentShip.Positions[0];

                // Ha ez a második pozíció, rögzítjük az irányt
                if (currentShip.Positions.Count == 1)
                {
                    if (firstPos.Row == row)
                    {
                        currentShip.IsRow = true;  // Sorban történik
                    }
                    else if (firstPos.Col == col)
                    {
                        currentShip.IsCol = true;  // Oszlopban történik
                    }
                }

                // Ellenőrizzük, hogy az új pozíció az irány mentén van-e
                if ((currentShip.IsRow && firstPos.Row != row) || (currentShip.IsCol && firstPos.Col != col))
                {
                    MessageBox.Show("A hajót csak az első két cella által meghatározott irányban (egy sorban vagy oszlopban) helyezheted el!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // A cella elhelyezése és beállítások
            map[row, col] = '1';
            (sender as Button).Background = Brushes.Blue;
            currentShip.Positions.Add((row, col));
            remainingCellsToPlace--;

            // Ha a hajó teljesen elhelyezett, beállításokat frissítünk
            if (remainingCellsToPlace == 0)
            {
                MarkAdjacentCells(map);
                ShipListBox.Items.Remove(ShipListBox.SelectedItem);
                ShipListBox.SelectedItem = null;
                isPlacingShip = false;
                firstSelection = true;
                currentShip = null;

                // Frissítjük a gomb láthatóságát
                CheckStartGameButtonVisibility();
            }
        }

        private string SerializeMap(char[,] map)
        {
            var rows = new List<string>();
            for (int row = 0; row < map.GetLength(0); row++)
            {
                var rowContent = new StringBuilder();
                for (int col = 0; col < map.GetLength(1); col++)
                {
                    // Replace null characters ('\0') with a placeholder (e.g., '0' for water)
                    rowContent.Append(map[row, col] == '\0' ? '0' : map[row, col]);
                }
                rows.Add(rowContent.ToString());
            }

            // Convert the rows list to JSON
            return JsonSerializer.Serialize(rows);
        }

        private async Task SendMapToServer(char[,] map)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                // Serialize the map
                var serializedMap = SerializeMap(map);

                // Deserialize the JSON string back into an object
                var mapObject = JsonSerializer.Deserialize<List<string>>(serializedMap);

                // Create a message object
                var message = new
                {
                    PlayerNumber = _playerNumber,
                    Map = mapObject
                };

                // Convert message to JSON with indented formatting
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true // Enable indented formatting
                };
                var messageJson = JsonSerializer.Serialize(message, options);

                // Send the message
                var buffer = Encoding.UTF8.GetBytes(messageJson);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private void MarkAdjacentCells(char[,] map)
        {
            foreach (var ship in placedShips)
            {
                foreach (var pos in ship.Positions)
                {
                    if (pos.Row > 0 && map[pos.Row - 1, pos.Col] == '\0') map[pos.Row - 1, pos.Col] = 'X';
                    if (pos.Row < 9 && map[pos.Row + 1, pos.Col] == '\0') map[pos.Row + 1, pos.Col] = 'X';
                    if (pos.Col > 0 && map[pos.Row, pos.Col - 1] == '\0') map[pos.Row, pos.Col - 1] = 'X';
                    if (pos.Col < 9 && map[pos.Row, pos.Col + 1] == '\0') map[pos.Row, pos.Col + 1] = 'X';
                }
            }
        }

        private bool IsAdjacentToSelected(char[,] map, int row, int col)
        {
            return (row > 0 && map[row - 1, col] == '1') ||
                   (row < 9 && map[row + 1, col] == '1') ||
                   (col > 0 && map[row, col - 1] == '1') ||
                   (col < 9 && map[row, col + 1] == '1');
        }

        private Button GetButtonFromGrid(Grid grid, int row, int col)
        {
            foreach (UIElement element in grid.Children)
            {
                if (element is Button button &&
                    Grid.GetRow(button) == row &&
                    Grid.GetColumn(button) == col)
                {
                    return button;
                }
            }

            // Ha nincs ilyen gomb
            throw new Exception($"A gomb a következő helyen nem található: sor {row}, oszlop {col}");
        }

        private async void StartGameButton_Click(object sender, RoutedEventArgs e)
        {
            StartGameButton.Visibility = Visibility.Collapsed;
            gameStarted = true;
            MessageBox.Show("A játék elkezdődött! Mostantól nem változtathatod meg a hajók elhelyezését.", "Játék", MessageBoxButton.OK, MessageBoxImage.Information);

            await SendMapToServer(leftMap);

            // Optionally, notify the player that the map was sent
            MessageBox.Show("Your map has been sent to the server!", "Game Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Player2 player = new Player2();
            player.Show();
            InitializeWebSocket();
        }
    }

}