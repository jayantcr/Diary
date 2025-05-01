using System.Text.Json;
using System.Diagnostics;

public class TextEditor : Form
{
    private MonthCalendar calendar;
    private RichTextBox textBox;
    private Label dateLabel;
    private string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TextEditor");
    private string currentDateFile;
    private DateTime currentDate;

    public TextEditor()
    {
        // Initialize UI components
        this.Text = "Diary";
        this.MinimumSize = new Size(800, 600);

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

        this.Resize += (sender, args) =>
        {
            textPanel.Width = this.ClientSize.Width - 250;
            textPanel.Height = this.ClientSize.Height;
        };

        // Date label at the top of the text box
        dateLabel = new Label
        {
            Left = 250,
            Top = 0
        };
        textPanel.Controls.Add(dateLabel);

        // Text box for editing
        textBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            DetectUrls = true,
            Left = 250,
            Top = 20,
            Width = this.ClientSize.Width - 250,
            Height = this.ClientSize.Height,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
        };
        textBox.LinkClicked += (sender, args) => Process.Start(new ProcessStartInfo(args.LinkText) { UseShellExecute = true });
        textPanel.Controls.Add(textBox);

        // Load data for the current date
        currentDate = DateTime.Now;
        LoadDataForDate(currentDate);

        // Ensure data directory exists
        if (!Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory);
        }

        // Save data when text box loses focus
        textBox.Leave += (sender, args) => SaveData();

        // Handle FormClosing event
        this.FormClosing += (sender, args) => SaveData();
    }

    private void Calendar_DateSelected(object sender, DateRangeEventArgs e)
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
            Entry entry = JsonSerializer.Deserialize<Entry>(json);
            textBox.Text = entry.Text;
        }
        else
        {
            textBox.Text = string.Empty;
        }
    }

    private void SaveData()
    {
        if (!string.IsNullOrEmpty(currentDateFile))
        {
            Entry entry = new Entry { Text = textBox.Text };
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

    private class Entry
    {
        public string Text { get; set; }
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TextEditor());
    }
}