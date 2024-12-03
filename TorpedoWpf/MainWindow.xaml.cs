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

        private bool isHorizontal = true;

        public MainWindow()
        {
            InitializeComponent();
            InitializeButtons(gPlayerField, leftMap, isLeftSide: true);
            InitializeButtons(gOpponentField, rightMap, isLeftSide: false);
            InitializeWebSocket();

            // DebugWindow Initialization
            if (DebugWindow.Instance == null)
            {
                new DebugWindow().Show(); // Open the debug window once
            }

            ShipListBox.SelectionChanged += ShipListBox_SelectionChanged;
            CheckStartGameButtonVisibility();
        }

        private async void InitializeWebSocket()
        {
            if (_webSocket != null && (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting))
            {
                DebugWindow.Instance.AppendMessage("WebSocket is already connected or connecting.");
                return;
            }

            try
            {
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri("ws://localhost:5000/"), CancellationToken.None);
                DebugWindow.Instance.AppendMessage($"Player {_playerNumber} connected to the server!");

                _ = ListenToServer(); // Start listening for server messages
            }
            catch (Exception ex)
            {
                DebugWindow.Instance.AppendMessage($"Failed to connect to server: {ex.Message}");
            }
        }

        private async Task ListenToServer()
        {
            var buffer = new byte[1024];

            try
            {
                while (_webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        DebugWindow.Instance.AppendMessage("Server has closed the connection.");
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        return;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleServerMessage(message);
                }
            }
            catch (WebSocketException ex)
            {
                DebugWindow.Instance.AppendMessage($"WebSocketException: {ex.Message}");
            }
            catch (Exception ex)
            {
                DebugWindow.Instance.AppendMessage($"Error receiving data: {ex.Message}");
            }
            finally
            {
                DebugWindow.Instance.AppendMessage("WebSocket connection closed.");
            }
        }
        private void HandleServerMessage(string message)
        {
            if (DebugWindow.Instance != null)
                DebugWindow.Instance.AppendMessage($"Received message: {message}");

            if (message.StartsWith("TURN:"))
            {
                int turn = int.Parse(message.Split(':')[1]);
                _currentTurn = turn;

                if (_playerNumber == _currentTurn)
                {
                    TurnLabel.Content = "Your Turn";
                    DebugWindow.Instance.AppendMessage("It's your turn!");
                }
                else
                {
                    TurnLabel.Content = "Opponent's Turn";
                    DebugWindow.Instance.AppendMessage("Waiting for opponent's move...");
                }
            }
            else if (message.StartsWith("SHOT_RESULT:"))
            {
                var resultData = message.Substring(12).Split(',');
                int row = int.Parse(resultData[0]);
                int col = int.Parse(resultData[1]);
                string result = resultData[2];

                if (_playerNumber == _currentTurn) // Shooter's perspective
                {
                    rightMap[row, col] = result == "HIT" ? 'H' : 'M';
                    UpdateGridCell(gOpponentField, row, col, result == "HIT" ? Brushes.Green : Brushes.Red);
                    DebugWindow.Instance.AppendMessage($"Shot at ({row},{col}) was a {result}.");
                }
                else // Defender's perspective
                {
                    leftMap[row, col] = result == "HIT" ? 'H' : 'M';
                    UpdateGridCell(gPlayerField, row, col, result == "HIT" ? Brushes.Green : Brushes.Red);
                    DebugWindow.Instance.AppendMessage($"Opponent shot at ({row},{col}) and it was a {result}.");
                }
            }
            else if (message == "Game Over!")
            {
                if (_currentTurn == _playerNumber)
                {
                    ShowGameOverMessage("Congratulation! You Won");
                    GameOverText.Foreground = Brushes.Green;
                }
                else
                {
                    ShowGameOverMessage("Game over! You lost");
                    GameOverText.Foreground = Brushes.Red;
                }
            }
            else
            {
                if (DebugWindow.Instance != null)
                    DebugWindow.Instance.AppendMessage($"Unhandled server message: {message}");
            }
        }





        private void UpdateGridCell(Grid grid, int row, int col, Brush color)
        {
            foreach (UIElement element in grid.Children)
            {
                if (Grid.GetRow(element) == row && Grid.GetColumn(element) == col)
                {
                    if (element is Button button)
                    {
                        button.Background = color;

                        if (DebugWindow.Instance != null)
                            DebugWindow.Instance.AppendMessage($"Updated cell at ({row}, {col}) to {color}.");
                        return;
                    }
                }
            }

            if (DebugWindow.Instance != null)
                DebugWindow.Instance.AppendMessage($"Button not found at ({row}, {col}).");
        }


        private async Task SendMessageAsync(string message)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    var buffer = Encoding.UTF8.GetBytes(message);
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (WebSocketException ex)
                {
                    DebugWindow.Instance.AppendMessage($"WebSocketException while sending: {ex.Message}");
                }
                catch (Exception ex)
                {
                    DebugWindow.Instance.AppendMessage($"Error sending data: {ex.Message}");
                }
            }
            else
            {
                DebugWindow.Instance.AppendMessage("Cannot send message: WebSocket is not open.");
            }
        }

        private void LoadMapFromData(List<string> mapData, char[,] map, Grid grid)
        {
            for (int row = 0; row < mapData.Count; row++)
            {
                for (int col = 0; col < mapData[row].Length; col++)
                {
                    map[row, col] = mapData[row][col];
                    Button button = GetButtonFromGrid(grid, row, col);
                    button.Background = map[row, col] == '1' ? Brushes.Blue : Brushes.LightGray;
                }
            }
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    DebugWindow.Instance.AppendMessage($"Error during WebSocket close: {ex.Message}");
                }
            }
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

                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, col);

                    int currentRow = row;
                    int currentCol = col;

                    if (isLeftSide)
                    {
                        button.Click += (sender, e) => Button_Click(sender, e, map, currentRow, currentCol);
                        button.MouseDoubleClick += Button_MouseDoubleClick;
                    }
                    else
                    {
                        button.Click += RightButtonGrid_Click;
                    }

                    grid.Children.Add(button);
                }
            }
        }


        private async void RightButtonGrid_Click(object sender, RoutedEventArgs e)
        {
            if (!gameStarted)
            {
                DebugWindow.Instance.AppendMessage("The game hasn't started yet.");
                return;
            }

            if (_currentTurn != _playerNumber)
            {
                DebugWindow.Instance.AppendMessage("It's not your turn!");
                return;
            }

            Button clickedButton = sender as Button;
            if (clickedButton == null) return;

            int row = Grid.GetRow(clickedButton);
            int col = Grid.GetColumn(clickedButton);

            if (rightMap[row, col] == 'H' || rightMap[row, col] == 'M')
            {
                DebugWindow.Instance.AppendMessage($"Already fired at ({row}, {col}). Choose another cell.");
                return;
            }

            await SendMessageAsync($"SHOT:{row},{col}");
            DebugWindow.Instance.AppendMessage($"Shot fired at ({row}, {col}). Waiting for opponent's move...");
        }

        private void ShipListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ShipListBox.SelectedItem != null)
            {
                SetShipLength();
            }
        }

        private void RemoveShipFromMap(int startRow, int startCol)
        {
            // Identify the ship at the clicked tile
            var ship = FindShipAtPosition(startRow, startCol);
            if (ship == null)
            {
                DebugWindow.Instance.AppendMessage("No ship found at the clicked position.");
                return;
            }

            // Remove the ship from the logical map and grid
            foreach (var position in ship.Positions)
            {
                int row = position.Row;
                int col = position.Col;
                leftMap[row, col] = '\0'; // Clear logical representation
                Button button = GetButtonFromGrid(gPlayerField, row, col);
                if (button != null)
                {
                    button.Background = Brushes.LightGray; // Reset visual color
                }
            }

            // Clear only the adjacent blocked tiles for this ship
            ClearAdjacentTilesForShip(leftMap, ship);

            // Add the ship back to the ListBox
            string shipDescription = $"{ship.Length} mező hosszú";
            ShipListBox.Items.Add(new ListBoxItem { Content = shipDescription });

            // Remove the ship from the placed list
            placedShips.Remove(ship);

            DebugWindow.Instance.AppendMessage($"Removed ship of length {ship.Length} from the map.");
        }



        private void SetShipLength()
        {
            var selectedItem = ShipListBox.SelectedItem as ListBoxItem;
            if (selectedItem != null)
            {
                // Extract ship length regardless of exact text format
                var match = Regex.Match(selectedItem.Content.ToString(), @"\d+");
                if (match.Success && int.TryParse(match.Value, out int length))
                {
                    remainingCellsToPlace = length;
                    DebugWindow.Instance.AppendMessage($"Ship length set to {remainingCellsToPlace}.");
                }
                else
                {
                    DebugWindow.Instance.AppendMessage("Failed to determine ship length. Please check the ship description.");
                }
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e, char[,] map, int row, int col)
        {
            if (!gameStarted)
            {
                DebugWindow.Instance.AppendMessage($"Attempting to place ship at ({row}, {col}).");
                DebugWindow.Instance.AppendMessage($"Selected ship length: {remainingCellsToPlace}");
            }


            if (gameStarted)
            {
                DebugWindow.Instance.AppendMessage("Cannot place or remove ships after the game has started.");
                return;
            }

            if (ShipListBox.SelectedItem == null)
            {
                DebugWindow.Instance.AppendMessage("Please select a ship before placing it.");
                return;
            }

            // Attempt placement in the default direction
            if (CanPlaceShip(map, row, col, remainingCellsToPlace, isHorizontal))
            {
                PlaceShip(map, row, col, remainingCellsToPlace, isHorizontal);
                DebugWindow.Instance.AppendMessage($"Player {_playerNumber} placed a ship at ({row}, {col}) {(isHorizontal ? "Horizontally" : "Vertically")}.");
            }
            // Try vertical placement if horizontal fails
            else if (CanPlaceShip(map, row, col, remainingCellsToPlace, !isHorizontal))
            {
                PlaceShip(map, row, col, remainingCellsToPlace, !isHorizontal);
                DebugWindow.Instance.AppendMessage($"Player {_playerNumber} placed a ship at ({row}, {col}) {(isHorizontal ? "Horizontally" : "Vertically")}.");
            }
            // Handle edge cases: upwards or leftwards placement
            else if (CanPlaceShip(map, Math.Max(row - remainingCellsToPlace + 1, 0), col, remainingCellsToPlace, false))
            {
                PlaceShip(map, Math.Max(row - remainingCellsToPlace + 1, 0), col, remainingCellsToPlace, false);
                DebugWindow.Instance.AppendMessage($"Ship placed upwards starting at ({row}, {col}).");
            }
            else if (CanPlaceShip(map, row, Math.Max(col - remainingCellsToPlace + 1, 0), remainingCellsToPlace, true))
            {
                PlaceShip(map, row, Math.Max(col - remainingCellsToPlace + 1, 0), remainingCellsToPlace, true);
                DebugWindow.Instance.AppendMessage($"Ship placed leftwards starting at ({row}, {col}).");
            }
            else
            {
                DebugWindow.Instance.AppendMessage($"Cannot place ship at ({row}, {col}). No valid position.");
                return;
            }

            // Remove the selected ship from the list after successful placement
            ShipListBox.Items.Remove(ShipListBox.SelectedItem);
            CheckStartGameButtonVisibility();
            DebugWindow.Instance.AppendMessage("Removed ship from list after successful placement.");
            ShipListBox.SelectedItem = null;
            remainingCellsToPlace = 0;
            isPlacingShip = false;

            DebugWindow.Instance.AppendMessage("Ship placed successfully.");
        }


        private void ClearAdjacentTilesForShip(char[,] map, Ship ship)
        {
            foreach (var position in ship.Positions)
            {
                int startRow = position.Row;
                int startCol = position.Col;

                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int adjRow = startRow + dr;
                        int adjCol = startCol + dc;

                        // Ensure within bounds
                        if (adjRow >= 0 && adjRow < map.GetLength(0) &&
                            adjCol >= 0 && adjCol < map.GetLength(1))
                        {
                            // Check if the tile is a blocked tile (not part of another ship)
                            if (map[adjRow, adjCol] == 'X')
                            {
                                // Verify the tile is not adjacent to another placed ship
                                if (!IsAdjacentToAnotherShip(map, adjRow, adjCol))
                                {
                                    map[adjRow, adjCol] = '\0'; // Clear logical marking
                                    Button button = GetButtonFromGrid(gPlayerField, adjRow, adjCol);
                                    if (button != null)
                                    {
                                        button.Background = Brushes.LightGray; // Reset visual marking
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool IsAdjacentToAnotherShip(char[,] map, int row, int col)
        {
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    int adjRow = row + dr;
                    int adjCol = col + dc;

                    // Ensure within bounds
                    if (adjRow >= 0 && adjRow < map.GetLength(0) &&
                        adjCol >= 0 && adjCol < map.GetLength(1))
                    {
                        if (map[adjRow, adjCol] == '1') // Another ship part detected
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool CanPlaceShip(char[,] map, int startRow, int startCol, int length, bool isHorizontal)
        {
            for (int i = 0; i < length; i++)
            {
                int row = isHorizontal ? startRow : startRow + i;
                int col = isHorizontal ? startCol + i : startCol;

                // Check grid bounds
                if (row < 0 || row >= map.GetLength(0) || col < 0 || col >= map.GetLength(1))
                {
                    DebugWindow.Instance.AppendMessage($"Placement failed: Ship would exceed grid bounds at ({row}, {col}).");
                    return false;
                }

                // Check the tile itself
                if (map[row, col] == '1')
                {
                    DebugWindow.Instance.AppendMessage($"Placement failed: Overlap detected at ({row}, {col}).");
                    return false;
                }

                // Check surrounding tiles for adjacency
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int adjRow = row + dr;
                        int adjCol = col + dc;

                        // Ensure adjacency checks are within bounds
                        if (adjRow >= 0 && adjRow < map.GetLength(0) && adjCol >= 0 && adjCol < map.GetLength(1))
                        {
                            if (map[adjRow, adjCol] == '1') // Adjacent ship detected
                            {
                                DebugWindow.Instance.AppendMessage($"Placement failed: Adjacent ship detected at ({adjRow}, {adjCol}).");
                                return false;
                            }
                        }
                    }
                }
            }

            return true; // Valid placement
        }


        private void PlaceShip(char[,] map, int startRow, int startCol, int length, bool isHorizontal)
        {
            var ship = new Ship
            {
                Length = length,
                IsRow = isHorizontal,
                Positions = new List<(int Row, int Col)>()
            };

            for (int i = 0; i < length; i++)
            {
                int row = isHorizontal ? startRow : startRow + i;
                int col = isHorizontal ? startCol + i : startCol;

                map[row, col] = '1';

                Button button = GetButtonFromGrid(gPlayerField, row, col);
                if (button != null)
                {
                    button.Background = Brushes.Blue;
                }

                ship.Positions.Add((row, col));
            }

            // Add ship to placedShips list
            placedShips.Add(ship);

            // Mark adjacent tiles as unavailable
            MarkAdjacentTiles(map, startRow, startCol, length, isHorizontal);
        }

        private Ship FindShipAtPosition(int row, int col)
        {
            return placedShips.FirstOrDefault(ship => ship.Positions.Any(pos => pos.Row == row && pos.Col == col));
        }


        private bool PlaceShipIfPossible(int row, int col, int length)
        {
            DebugWindow.Instance.AppendMessage($"Attempting to place ship at ({row}, {col}) in {(isHorizontal ? "horizontal" : "vertical")} orientation.");

            // Validate placement
            if (!CanPlaceShip(leftMap, row, col, length, isHorizontal))
            {
                DebugWindow.Instance.AppendMessage("Placement failed due to invalid position.");
                return false;
            }

            // Place the ship
            PlaceShip(leftMap,row, col, length, isHorizontal);
            DebugWindow.Instance.AppendMessage($"Ship placed at ({row}, {col}) {(isHorizontal ? "horizontally" : "vertically")}.");
            return true;
        }


        private string SerializeMap(char[,] map)
        {
            var mapList = new List<string>();
            for (int row = 0; row < 10; row++)
            {
                var rowString = new StringBuilder();
                for (int col = 0; col < 10; col++)
                {
                    rowString.Append(map[row, col] == '\0' ? 'E' : map[row, col]);
                }
                mapList.Add(rowString.ToString());
            }
            return JsonSerializer.Serialize(mapList);
        }

        private async Task SendMapToServer(char[,] map)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    // Convert the map into a list of strings
                    var mapList = new List<string>();
                    for (int row = 0; row < 10; row++)
                    {
                        var rowString = new StringBuilder();
                        for (int col = 0; col < 10; col++)
                        {
                            rowString.Append(map[row, col] == '\0' ? 'E' : map[row, col]);
                        }
                        mapList.Add(rowString.ToString());
                    }

                    // Create the message object
                    var message = new
                    {
                        PlayerNumber = _playerNumber,
                        Map = mapList
                    };

                    // Serialize the message to JSON
                    var messageJson = JsonSerializer.Serialize(message);

                    // Add the "MAP:" prefix
                    var prefixedMessage = $"MAP:{messageJson}";

                    // Convert the prefixed message into a byte array and send it over the WebSocket
                    var buffer = Encoding.UTF8.GetBytes(prefixedMessage);
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);

                    DebugWindow.Instance.AppendMessage($"Serialized Map sent: {prefixedMessage}");
                }
                catch (Exception ex)
                {
                    DebugWindow.Instance.AppendMessage($"Error sending map: {ex.Message}");
                }
            }
        }



        private Button GetButtonFromGrid(Grid grid, int row, int col)
        {
            foreach (UIElement element in grid.Children)
            {
                if (Grid.GetRow(element) == row && Grid.GetColumn(element) == col)
                {
                    return element as Button;
                }
            }
            return null;
        }

        private void OnGridCellClick(object sender, RoutedEventArgs e)
        {
            if (ShipListBox.SelectedItem == null)
            {
                DebugWindow.Instance.AppendMessage("No ship selected for placement.");
                return;
            }

            Button clickedButton = sender as Button;
            if (clickedButton == null)
                return;

            int row = Grid.GetRow(clickedButton);
            int col = Grid.GetColumn(clickedButton);

            int shipLength = int.Parse(ShipListBox.SelectedItem.ToString()); // Assuming list box stores ship lengths

            if (PlaceShipIfPossible(row, col, shipLength))
            {
                ShipListBox.Items.Remove(ShipListBox.SelectedItem); // Remove ship from list after successful placement
                DebugWindow.Instance.AppendMessage("Removed ship from list after successful placement.");
            }
        }

        private void MarkAdjacentTiles(char[,] map, int startRow, int startCol, int length, bool isHorizontal)
        {
            for (int i = -1; i <= length; i++)
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int row = isHorizontal ? startRow + dr : startRow + i;
                        int col = isHorizontal ? startCol + i : startCol + dc;

                        // Ensure within bounds
                        if (row >= 0 && row < map.GetLength(0) && col >= 0 && col < map.GetLength(1))
                        {
                            // Skip the ship itself
                            if (map[row, col] == '1')
                                continue;

                            // Mark as unavailable
                            map[row, col] = 'X'; // Logical marking
                            Button button = GetButtonFromGrid(gPlayerField, row, col);
                            if (button != null)
                            {
                                button.Background = Brushes.DarkGray; // Visual marking
                            }
                        }
                    }
                }
            }
        }

        private async void StartGameButton_Click(object sender, RoutedEventArgs e)
        {
            ShipListBox.IsEnabled = false;
            SetGridButtonsEnabled(gOpponentField, true);
            RemovePlayerFieldDoubleClick();
            StartGameButton.Visibility = Visibility.Collapsed;
            ShipListBox.Visibility = Visibility.Collapsed;
            Megmaradhajok.Visibility = Visibility.Collapsed;
            ToggleOrientationButton.Visibility = Visibility.Collapsed;
            gameStarted = true;

            await SendMapToServer(leftMap);
            DebugWindow.Instance.AppendMessage("Map sent to the server. Waiting for opponent.");

            // Send READY message
            await SendMessageAsync("READY");
        }

        private void ShowGameOverMessage(string message)
        {
            GameOverText.Text = message;
            GameOverText.Visibility = Visibility.Visible;
        }

        private void Button_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Button button = sender as Button;
            int row = Grid.GetRow(button);
            int col = Grid.GetColumn(button);

            RemoveShipFromMap(row, col);
        }

        private void RemovePlayerFieldDoubleClick()
        {
            foreach (UIElement element in gPlayerField.Children)
            {
                if (element is Button button)
                {
                    button.MouseDoubleClick -= Button_MouseDoubleClick;
                }
            }
        }

        private void SetGridButtonsEnabled(Grid grid, bool isEnabled)
        {
            foreach (UIElement element in grid.Children)
            {
                if (element is Button button)
                {
                    button.IsEnabled = isEnabled; // Only disable the button without resetting other properties
                }
            }
        }

        private void DebugMapAndGrid()
        {
            DebugWindow.Instance.AppendMessage("Debugging leftMap:");
            for (int row = 0; row < 10; row++)
            {
                var line = new StringBuilder();
                for (int col = 0; col < 10; col++)
                {
                    line.Append(leftMap[row, col]);
                }
                DebugWindow.Instance.AppendMessage(line.ToString());
            }

            DebugWindow.Instance.AppendMessage("Debugging player grid button backgrounds:");
            foreach (UIElement element in gPlayerField.Children)
            {
                if (element is Button button)
                {
                    int row = Grid.GetRow(button);
                    int col = Grid.GetColumn(button);
                    DebugWindow.Instance.AppendMessage($"Button[{row},{col}]: {button.Background}");
                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Player2 player = new Player2();
            player.Show();
            InitializeWebSocket();
        }

        private void ToggleOrientationButton_Click(object sender, RoutedEventArgs e)
        {
            isHorizontal = !isHorizontal;
            ToggleOrientationButton.Content = isHorizontal ? "Switch to Vertical" : "Switch to Horizontal";
            DebugWindow.Instance.AppendMessage($"Ship orientation switched to {(isHorizontal ? "horizontal" : "vertical")}.");
        }
    }

}