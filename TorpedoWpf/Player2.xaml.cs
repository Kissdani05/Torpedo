using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TorpedoWpf
{
    /// <summary>
    /// Interaction logic for Player2.xaml
    /// </summary>
    

    public partial class Player2 : Window
    {
        private char[,] leftMap = new char[10, 10];
        private char[,] rightMap = new char[10, 10];

        private bool firstSelection = true;
        private int remainingCellsToPlace = 0;
        private bool isPlacingShip = false;
        private bool gameStarted = false;

        private Ship currentShip = null;
        private List<Ship> placedShips = new List<Ship>();

        public Player2()
        {
            InitializeComponent();
            InitializeButtons(gPlayerField, leftMap, isLeftSide: true);
            InitializeButtons(gOpponentField, rightMap, isLeftSide: false);
            InitializeLeftMap();

            // ListBox kiválasztásának eseménykezelője
            ShipListBox.SelectionChanged += ShipListBox_SelectionChanged;

            // Ellenőrizzük a gomb láthatóságát az inicializáláskor is
            CheckStartGameButtonVisibility();
        }
        private void CheckStartGameButtonVisibility()
        {
            // Ha nincs elem a ShipListBox-ban, akkor a gomb megjelenik, különben elrejtjük
            StartGameButton.Visibility = ShipListBox.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        private void InitializeLeftMap()
        {
            // Ellenfél hajóinak elhelyezése (teszteléshez)
            leftMap[2, 3] = '1'; // Hajó 1. része
            leftMap[2, 4] = '1'; // Hajó 1. része
            leftMap[5, 6] = '1'; // Hajó 2. része
            leftMap[7, 8] = '1'; // Hajó 3. része
            leftMap[9, 1] = '1'; // Hajó 4. része
                                 // Adjon hozzá további adatokat teszteléshez
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
                        // Kezdetben a bal oldali gombokat letiltjuk, később a játék indításakor aktiváljuk
                        button.IsEnabled = false;
                        button.Click += (sender, e) => LeftButtonGrid_Click(sender, e); // Az ellenfél hajóira céloz
                    }
                    else
                    {
                        button.Click += (sender, e) => Button_Click(sender, e, map, currentRow, currentCol);
                    }

                    // Gomb hozzáadása a Gridhez
                    grid.Children.Add(button);
                }
            }
        }


        private void LeftButtonGrid_Click(object sender, RoutedEventArgs e)
        {
            if (!gameStarted)
            {
                MessageBox.Show("A játék még nem kezdődött el, nem lőhetsz az ellenfél mezőire!", "Figyelmeztetés", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Button clickedButton = sender as Button;
            int row = Grid.GetRow(clickedButton);
            int col = Grid.GetColumn(clickedButton);

            if (leftMap[row, col] == '1') // Találat
            {
                clickedButton.Background = Brushes.Green;
                leftMap[row, col] = 'H'; // Jelöljük a találatot
                MessageBox.Show("Találat! Eltaláltad az ellenfél hajóját!", "Találat", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (leftMap[row, col] == '\0') // Tévesztés
            {
                clickedButton.Background = Brushes.Red;
                leftMap[row, col] = 'M'; // Jelöljük a tévesztést
                MessageBox.Show("Tévesztettél! Nincs hajó ezen a mezőn.", "Tévesztés", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show("Már lőttél erre a mezőre! Válassz másikat.", "Figyelmeztetés", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
                MessageBox.Show("A játék elindult, nem változtathatod meg a hajók elhelyezését!", "Figyelmeztetés", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ship = placedShips.Find(s => s.Positions.Contains((row, col)));
            if (ship != null)
            {
                // Hajó eltávolítása a térképről és a listából
                foreach (var pos in ship.Positions)
                {
                    map[pos.Row, pos.Col] = '\0'; // A mező visszaállítása üresre
                    GetButtonFromGrid(gOpponentField, pos.Row, pos.Col).Background = Brushes.LightGray; // A gomb színének visszaállítása
                }

                // Hajó eltávolítása a listából
                placedShips.Remove(ship);

                // A hajó visszakerül a listába a ShipListBox-ban
                var shipItem = new ListBoxItem { Content = $"{ship.Length} mező hosszú" };
                ShipListBox.Items.Add(shipItem);

                // Visszaállítjuk a Játék gombot, ha szükséges
                CheckStartGameButtonVisibility();

                // Frissítjük a szomszédos mezők szabad állapotát, hogy újra le lehessen rakni őket
                MarkAdjacentCells(map);

                // Eltávolítjuk az összes "X" jelet, hogy ne befolyásolják a későbbi elhelyezést
                ClearAdjacentMarkings(map);

                return;
            }

            // Hajó elhelyezése
            if (ShipListBox.SelectedItem != null && map[row, col] != '1' && map[row, col] != 'X')
            {
                if (firstSelection)
                {
                    PlaceShip(sender, map, row, col);
                    firstSelection = false;
                }
                else if (IsAdjacentToSelected(map, row, col))
                {
                    PlaceShip(sender, map, row, col);
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

        private void StartGameButton_Click(object sender, RoutedEventArgs e)
        {
            StartGameButton.Visibility = Visibility.Collapsed;
            gameStarted = true;

            // Bal oldali mezők aktiválása
            foreach (Button button in gPlayerField.Children.OfType<Button>())
            {
                button.IsEnabled = true;
            }

            MessageBox.Show("A játék elkezdődött! Mostantól támadhatod az ellenfél hajóit.", "Játék", MessageBoxButton.OK, MessageBoxImage.Information);
        }

    }
}
