using System.Text.Json;
using System.Diagnostics;
using System.Windows.Forms;

public class TextEditor : Form
{
    private MonthCalendar calendar;
    private Label dateLabel;
    private string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoodVibesDiary");
    private string currentDateFile = string.Empty;
    private DateTime currentDate;
    private TextBox lineNumberTextBox;
    private RichTextBox richTextBox;

    private class Entry
    {
        public required string Text { get; set; }
    }

    private class SearchResult
    {
        public DateTime Date { get; set; }
        public required string Text { get; set; }
        public required string Query { get; set; }

        public override string ToString()
        {
            return Date.ToString("yyyy-MM-dd");
        }
    }

    private class SearchIndex
    {
        private readonly Dictionary<string, HashSet<string>> wordIndex = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> fileContents = new(StringComparer.OrdinalIgnoreCase);
        private DateTime lastIndexUpdate = DateTime.MinValue;
        private readonly string dataDirectory;

        public SearchIndex(string dataDirectory) => this.dataDirectory = dataDirectory;

        public void UpdateIndex()
        {
            if ((DateTime.Now - lastIndexUpdate).TotalMinutes < 5)
                return;

            wordIndex.Clear();
            fileContents.Clear();

            foreach (string file in Directory.GetFiles(dataDirectory, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    if (JsonSerializer.Deserialize<Entry>(json) is Entry entry)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        fileContents[fileName] = entry.Text;
                        
                        string[] words = entry.Text.Split(new[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?' }, 
                            StringSplitOptions.RemoveEmptyEntries);
                        foreach (string word in words)
                        {
                            string normalizedWord = word.ToLowerInvariant();
                            if (!wordIndex.ContainsKey(normalizedWord))
                                wordIndex[normalizedWord] = new();
                            wordIndex[normalizedWord].Add(fileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error indexing file {file}: {ex.Message}");
                }
            }
            lastIndexUpdate = DateTime.Now;
        }

        public List<SearchResult> Search(string query)
        {
            UpdateIndex();
            if (string.IsNullOrWhiteSpace(query))
                return new();

            string normalizedQuery = query.ToLowerInvariant();
            HashSet<string> matchingFiles = new();

            if (wordIndex.ContainsKey(normalizedQuery))
                matchingFiles.UnionWith(wordIndex[normalizedQuery]);

            foreach (var word in wordIndex.Keys)
            {
                if (word.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) || 
                    normalizedQuery.Contains(word, StringComparison.OrdinalIgnoreCase))
                    matchingFiles.UnionWith(wordIndex[word]);
            }

            return matchingFiles
                .Where(fileName => fileContents.TryGetValue(fileName, out _))
                .Select(fileName => new SearchResult 
                { 
                    Date = DateTime.ParseExact(fileName, "yyyy-MM-dd", null),
                    Text = fileContents[fileName],
                    Query = query
                })
                .OrderBy(r => r.Date)
                .ToList();
        }
    }

    private SearchIndex? searchIndex;

    public TextEditor()
    {
        // Initialize UI components
        this.Text = "Good Vibes Diary";
        this.MinimumSize = new Size(800, 600);
        this.Icon = new Icon("Resources\\icon.ico");

        // Panel for calendar and navigation buttons
        Panel calendarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 250,
        };
        this.Controls.Add(calendarPanel);

        // Navigation buttons for calendar
        Button prevMonthButton = new Button
        {
            Location = new Point(10, 10),
            Text = "<",
            Width = 20,
        };
        prevMonthButton.Click += (sender, args) => PrevMonth();
        calendarPanel.Controls.Add(prevMonthButton);

        Button nextMonthButton = new Button
        {
            Location = new Point(220, 10),
            Text = ">",
            Width = 20,
        };
        nextMonthButton.Click += (sender, args) => NextMonth();
        calendarPanel.Controls.Add(nextMonthButton);

        // Calendar
        calendar = new MonthCalendar
        {
            Location = new Point(10, 50),
            Width = 220,
            MaxSelectionCount = 1,
        };
        calendar.DateSelected += Calendar_DateSelected;
        calendarPanel.Controls.Add(calendar);

        // Panel for text editor
        Panel textPanel = new Panel
        {
            Dock = DockStyle.Fill,
        };
        this.Controls.Add(textPanel);

        // Search panel
        Panel searchPanel = new Panel
        {
            Location = new Point(10, 250),
            Width = 240,
            Height = 300
        };
        calendarPanel.Controls.Add(searchPanel);

        // Search box
        TextBox searchBox = new TextBox
        {
            Location = new Point(0, 0),
            Width = 150,
        };
        searchPanel.Controls.Add(searchBox);

        // Search button
        Button searchButton = new Button
        {
            Location = new Point(155, 0),
            Text = "Search",
            Width = 65,
        };
        searchPanel.Controls.Add(searchButton);

        // Add Enter key handler to search box
        searchBox.KeyPress += (sender, e) =>
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true; // Prevent the beep sound
                searchButton.PerformClick();
            }
        };

        // List box for search results
        ListBox listBox = new ListBox
        {
            Location = new Point(0, 60),
            Width = 230,
            Height = 280,
        };
        listBox.SelectedIndexChanged += (sender, args) =>
        {
            if (listBox.SelectedItem != null)
            {
                SearchResult result = (SearchResult)listBox.SelectedItem;
                LoadDataForDate(result.Date);
                HighlightSearchResult(result);
            }
        };
        searchPanel.Controls.Add(listBox);

        // Initialize search index
        searchIndex = new SearchIndex(dataDirectory);

        // Update search button click handler
        searchButton.Click += (sender, args) =>
        {
            string query = searchBox.Text;
            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show("Please enter a search term.", "Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Cursor = Cursors.WaitCursor;
            try
            {
                List<SearchResult> results = searchIndex?.Search(query) ?? new List<SearchResult>();
                listBox.Items.Clear();
                foreach (SearchResult result in results)
                {
                    listBox.Items.Add(result);
                }
                if (results.Count == 0)
                {
                    MessageBox.Show("No matches found.", "Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        };

        // Clear button
        Button clearButton = new Button
        {
            Location = new Point(155, 30),
            Text = "Clear",
            Width = 65,
        };
        clearButton.Click += (sender, args) =>
        {
            searchBox.Text = string.Empty;
            listBox.Items.Clear();
            RemoveHighlighting();
        };
        searchPanel.Controls.Add(clearButton);

        // Date label at the top of the text box
        dateLabel = new Label
        {
            Left = 250,
            Top = 0
        };
        textPanel.Controls.Add(dateLabel);

        // Line number text box
        lineNumberTextBox = new TextBox
        {
            Location = new Point(250, 25),
            Width = 40,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.None,
            WordWrap = false,
            Height = this.ClientSize.Height,
            Font = new Font("Consolas", 11)
        };
        textPanel.Controls.Add(lineNumberTextBox);

        // Text box for editing
        richTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            DetectUrls = true,
            Left = 290,
            Top = 24,
            Width = this.ClientSize.Width - 210,
            Height = this.ClientSize.Height,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
            Font = new Font("Consolas", 11)
        };
        richTextBox.LinkClicked += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.LinkText))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(args.LinkText) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open link: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("The link is empty or null.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        textPanel.Controls.Add(richTextBox);

        UpdateLineNumbers(lineNumberTextBox, richTextBox);

        // Update line numbers
        richTextBox.TextChanged += (sender, args) =>
        {
            UpdateLineNumbers(lineNumberTextBox, richTextBox);
        };

        // Synchronize scrolling
        richTextBox.VScroll += (sender, args) =>
        {
            lineNumberTextBox.SelectionStart = richTextBox.GetFirstCharIndexFromLine(richTextBox.GetLineFromCharIndex(richTextBox.SelectionStart));
            lineNumberTextBox.ScrollToCaret();
        };

        // Load data for the current date
        currentDate = DateTime.Now;
        LoadDataForDate(currentDate);

        // Ensure data directory exists
        if (!Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory);
        }

        // Save data when text box loses focus
        richTextBox.Leave += (sender, args) => SaveData();

        this.Resize += (sender, args) =>
        {
            textPanel.Width = this.ClientSize.Width - 250;
            textPanel.Height = this.ClientSize.Height;
            UpdateLineNumbers(lineNumberTextBox, richTextBox);
        };

        // Handle FormClosing event
        this.FormClosing += (sender, args) => SaveData();
    }

    private void UpdateLineNumbers(TextBox lineNumberTextBox, RichTextBox textBox)
    {
        lineNumberTextBox.Clear();
        var lineNumbers = new List<string>();
        int lineNumber = 1;
        int lineCount = 1;
        int lastCharIndex = 0;
        
        foreach (var line in textBox.Lines)
        {
            lineNumbers.Add(lineCount.ToString());
            if(line.Length > 0)
            {
                lastCharIndex += line.Length;
                int lastCharLineNumber = textBox.GetLineFromCharIndex(lastCharIndex) + 1;
                while (lastCharLineNumber > lineNumber)
                {
                    lineNumbers.Add("");
                    lineNumber++;
                }
            }
            lineNumber++;
            lineCount++;
        }

        lineNumberTextBox.Text = string.Join(Environment.NewLine, lineNumbers);
        lineNumberTextBox.Multiline = true;
        lineNumberTextBox.WordWrap = false;
        lineNumberTextBox.Height = textBox.Height;
    }

    private void HighlightSearchResult(SearchResult result)
    {
        richTextBox.SelectAll();
        richTextBox.SelectionColor = richTextBox.ForeColor;
        richTextBox.SelectionBackColor = richTextBox.BackColor;
        richTextBox.DeselectAll();

        int index = 0;
        while ((index = richTextBox.Text.IndexOf(result.Query, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            richTextBox.Select(index, result.Query.Length);
            richTextBox.SelectionColor = Color.Black;
            richTextBox.SelectionBackColor = Color.Yellow;
            index += result.Query.Length;
        }
        richTextBox.Select(0, 0);
    }

    private void RemoveHighlighting()
    {
        richTextBox.SelectAll();
        richTextBox.SelectionColor = richTextBox.ForeColor;
        richTextBox.SelectionBackColor = richTextBox.BackColor;
        richTextBox.DeselectAll();
    }

    private void Calendar_DateSelected(object? sender, DateRangeEventArgs e)
    {
        LoadDataForDate(e.Start);
    }

    private void LoadDataForDate(DateTime date)
    {
        string dateString = date.ToString("yyyy-MM-dd");
        currentDateFile = Path.Combine(dataDirectory, dateString + ".json");
        dateLabel.Text = date.ToString("yyyy-MM-dd");
        currentDate = date;

        if (File.Exists(currentDateFile))
        {
            string json = File.ReadAllText(currentDateFile);
            Entry? entry = JsonSerializer.Deserialize<Entry>(json);
            if (entry != null) // Ensure entry is not null before accessing its properties
            {
                richTextBox.Text = entry.Text;
            }
            else
            {
                richTextBox.Text = string.Empty; // Handle case where deserialization returns null
            }
        }
        else
        {
            richTextBox.Text = string.Empty;
        }
    }

    private void SaveData()
    {
        if (!string.IsNullOrEmpty(currentDateFile))
        {
            Entry entry = new Entry { Text = richTextBox.Text };
            string json = JsonSerializer.Serialize(entry);
            File.WriteAllText(currentDateFile, json);
        }
    }

    private void PrevMonth()
    {
        DateTime newDate = currentDate.AddMonths(-1);
        calendar.SetDate(newDate);
        LoadDataForDate(newDate);
    }

    private void NextMonth()
    {
        DateTime newDate = currentDate.AddMonths(1);
        calendar.SetDate(newDate);
        LoadDataForDate(newDate);
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TextEditor());
    }
}