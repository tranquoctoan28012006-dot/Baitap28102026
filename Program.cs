using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

namespace DangKyKhoaHoc;

// =====================================================
//  Kết nối CSDL
// =====================================================
public static class Db
{
    // Sửa Server cho đúng máy bạn:
    //   localhost | .\SQLEXPRESS | (localdb)\MSSQLLocalDB | TÊN-MÁY\SQLEXPRESS
    public const string ConnStr =
        @"Server=.\SQLEXPRESS;Database=ql_sinhvien;Integrated Security=True;TrustServerCertificate=True;";

    // Chỉ truyền câu SQL cố định do lập trình viên viết, không nhận dữ liệu từ người dùng
    public static async Task<DataTable> GetTableAsync(string sql)
    {
        var table = new DataTable();
        await using var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var rd = await cmd.ExecuteReaderAsync();
        table.Load(rd);
        return table;
    }
}

// =====================================================
//  Đăng ký
// =====================================================
public record DangKy(
    string HoTen,
    string GioiTinh,
    DateTime NgaySinh,
    string SoDienThoai,
    int MaKH,
    string? TuyChon);

public static class DangKyService
{
    public static Task<DataTable> GetCoursesAsync() =>
        Db.GetTableAsync("SELECT MaKH, TenKH, HocPhi FROM KhoaHoc ORDER BY MaKH");

    public static Task<DataTable> GetRegistrationsAsync() =>
        Db.GetTableAsync(
            "SELECT TOP 100 d.Id AS [Mã ĐK], d.HoTen AS [Họ tên], d.GioiTinh AS [Giới tính], " +
            "d.NgaySinh AS [Ngày sinh], d.SoDienThoai AS [SĐT], k.TenKH AS [Khóa học], " +
            "d.TuyChon AS [Đăng ký thêm], d.NgayDangKy AS [Ngày ĐK] " +
            "FROM DangKy d JOIN KhoaHoc k ON k.MaKH = d.MaKH ORDER BY d.Id DESC");

    // Lưu đăng ký, trả về mã đăng ký vừa tạo
    public static async Task<int> AddAsync(DangKy dk)
    {
        const string sql =
            "INSERT INTO DangKy (HoTen, GioiTinh, NgaySinh, SoDienThoai, MaKH, TuyChon) " +
            "VALUES (@ten, @gt, @ns, @sdt, @ma, @tc); " +
            "SELECT CAST(SCOPE_IDENTITY() AS INT);";

        await using var conn = new SqlConnection(Db.ConnStr);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@ten", SqlDbType.NVarChar, 100).Value = dk.HoTen;
        cmd.Parameters.Add("@gt", SqlDbType.NVarChar, 10).Value = dk.GioiTinh;
        cmd.Parameters.Add("@ns", SqlDbType.Date).Value = dk.NgaySinh;
        cmd.Parameters.Add("@sdt", SqlDbType.VarChar, 20).Value = dk.SoDienThoai;
        cmd.Parameters.Add("@ma", SqlDbType.Int).Value = dk.MaKH;
        cmd.Parameters.Add("@tc", SqlDbType.NVarChar, 200).Value = (object?)dk.TuyChon ?? DBNull.Value;

        await conn.OpenAsync();
        object? id = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(id);
    }
}

// =====================================================
//  Form đăng ký (tạo control bằng code)
// =====================================================
public class frmDangKy : Form
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    private readonly TextBox txtFullName = new()
    {
        Name = "txtFullName",
        Location = new Point(130, 27),
        Size = new Size(300, 23),
        MaxLength = 100,
        TabIndex = 0
    };

    // Giới tính: 2 RadioButton đặt chung 1 Panel nên chỉ chọn được 1
    private readonly Panel pnlGender = new() { Location = new Point(130, 62), Size = new Size(220, 26) };
    private readonly RadioButton rdoMale = new() { Name = "rdoMale", Text = "Nam", Location = new Point(0, 2), AutoSize = true, Checked = true, TabIndex = 0 };
    private readonly RadioButton rdoFemale = new() { Name = "rdoFemale", Text = "Nữ", Location = new Point(80, 2), AutoSize = true, TabIndex = 1 };

    // Ngày sinh: Format Short (dd/MM/yyyy trên Windows tiếng Việt)
    private readonly DateTimePicker dtpBirthDate = new()
    {
        Name = "dtpBirthDate",
        Location = new Point(130, 99),
        Size = new Size(150, 23),
        Format = DateTimePickerFormat.Short,
        MinDate = new DateTime(1900, 1, 1),
        MaxDate = DateTime.Today,
        Value = new DateTime(2005, 1, 1),
        TabIndex = 2
    };

    // SĐT: Mask (000) 000-0000
    private readonly MaskedTextBox mtxtPhone = new()
    {
        Name = "mtxtPhone",
        Location = new Point(130, 135),
        Size = new Size(150, 23),
        Mask = "(000) 000-0000",
        PromptChar = '_',
        TextMaskFormat = MaskFormat.IncludeLiterals,   // Text trả về dạng (090) 123-4567
        TabIndex = 3
    };

    private readonly ComboBox cboCourse = new()
    {
        Name = "cboCourse",
        Location = new Point(130, 171),
        Size = new Size(300, 23),
        DropDownStyle = ComboBoxStyle.DropDownList,
        TabIndex = 4
    };

    private readonly CheckBox chkEnglish = new() { Name = "chkEnglish", Text = "Học thêm Tiếng Anh", Location = new Point(130, 208), AutoSize = true, TabIndex = 5 };
    private readonly CheckBox chkSoftSkill = new() { Name = "chkSoftSkill", Text = "Học thêm Kỹ năng mềm", Location = new Point(130, 232), AutoSize = true, TabIndex = 6 };
    private readonly CheckBox chkInternship = new() { Name = "chkInternship", Text = "Đăng ký thực tập tại doanh nghiệp", Location = new Point(130, 256), AutoSize = true, TabIndex = 7 };

    private readonly Button btnRegister = new() { Name = "btnRegister", Text = "Đăng ký", Location = new Point(12, 322), Size = new Size(110, 34), TabIndex = 8 };
    private readonly Button btnReset = new() { Name = "btnReset", Text = "Nhập lại", Location = new Point(132, 322), Size = new Size(110, 34), CausesValidation = false, TabIndex = 9 };
    private readonly Button btnExit = new() { Name = "btnExit", Text = "Thoát", Location = new Point(252, 322), Size = new Size(110, 34), CausesValidation = false, TabIndex = 10 };

    private readonly DataGridView dgvRegistrations = new()
    {
        Location = new Point(12, 372),
        Size = new Size(596, 256),
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
        BackgroundColor = SystemColors.Window
    };

    private readonly ErrorProvider errorProvider = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public frmDangKy()
    {
        Text = "Đăng ký khóa học";
        ClientSize = new Size(620, 640);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        errorProvider.ContainerControl = this;

        // Nếu Windows của bạn không đặt định dạng ngày là dd/MM/yyyy,
        // bỏ comment 2 dòng dưới để ép đúng định dạng:
        // dtpBirthDate.Format = DateTimePickerFormat.Custom;
        // dtpBirthDate.CustomFormat = "dd/MM/yyyy";

        pnlGender.Controls.AddRange(new Control[] { rdoMale, rdoFemale });

        var grpInfo = new GroupBox
        {
            Text = "Thông tin đăng ký",
            Location = new Point(12, 10),
            Size = new Size(596, 300)
        };
        grpInfo.Controls.AddRange(new Control[]
        {
            Lbl("Họ tên:", 20, 27),          txtFullName,
            Lbl("Giới tính:", 20, 63),       pnlGender,
            Lbl("Ngày sinh:", 20, 99),       dtpBirthDate,
            Lbl("Số điện thoại:", 20, 135),  mtxtPhone,
            Lbl("Khóa học:", 20, 171),       cboCourse,
            Lbl("Đăng ký thêm:", 20, 208),   chkEnglish, chkSoftSkill, chkInternship
        });

        Controls.AddRange(new Control[] { grpInfo, btnRegister, btnReset, btnExit, dgvRegistrations });

        // ----- Sự kiện -----
        btnRegister.Click += btnRegister_Click;
        btnReset.Click += (_, _) => ResetForm();
        btnExit.Click += (_, _) => Close();

        txtFullName.TextChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(txtFullName.Text))
                errorProvider.SetError(txtFullName, string.Empty);
        };
        mtxtPhone.TextChanged += (_, _) =>
        {
            if (mtxtPhone.MaskCompleted)
                errorProvider.SetError(mtxtPhone, string.Empty);
        };
        cboCourse.SelectedIndexChanged += (_, _) =>
        {
            if (cboCourse.SelectedIndex > 0)
                errorProvider.SetError(cboCourse, string.Empty);
        };

        AcceptButton = btnRegister;   // Enter
        CancelButton = btnExit;       // Esc

        // Nạp khóa học + danh sách đăng ký khi form hiện ra
        Shown += async (_, _) => await LoadDataAsync();
    }

    private static Label Lbl(string text, int x, int y) =>
        new() { Text = text, Location = new Point(x, y + 3), AutoSize = true };

    // ----- Nạp dữ liệu từ CSDL -----
    private async Task LoadDataAsync()
    {
        try
        {
            // Binding Data cho ComboBox: DisplayMember / ValueMember
            DataTable courses = await DangKyService.GetCoursesAsync();

            // Dòng gợi ý ở đầu danh sách (MaKH = 0 nghĩa là chưa chọn)
            DataRow placeholder = courses.NewRow();
            placeholder["MaKH"] = 0;
            placeholder["TenKH"] = "-- Chọn khóa học --";
            placeholder["HocPhi"] = 0m;
            courses.Rows.InsertAt(placeholder, 0);

            cboCourse.DisplayMember = "TenKH";   // chữ hiển thị
            cboCourse.ValueMember = "MaKH";      // giá trị thật
            cboCourse.DataSource = courses;
            cboCourse.SelectedIndex = 0;

            await LoadRegistrationsAsync();
        }
        catch (SqlException ex)
        {
            MessageBox.Show($"Không tải được dữ liệu:\n{ex.Message}", "Lỗi CSDL",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadRegistrationsAsync()
    {
        dgvRegistrations.DataSource = await DangKyService.GetRegistrationsAsync();

        if (dgvRegistrations.Columns["Ngày sinh"] is { } c1)
            c1.DefaultCellStyle.Format = "dd/MM/yyyy";
        if (dgvRegistrations.Columns["Ngày ĐK"] is { } c2)
            c2.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
    }

    // ----- Kiểm tra dữ liệu bằng ErrorProvider -----
    private bool Check(Control c, bool hasError, string message)
    {
        errorProvider.SetError(c, hasError ? message : string.Empty);
        return !hasError;
    }

    private bool ValidateInput()
    {
        bool ok = true;

        ok &= Check(txtFullName, string.IsNullOrWhiteSpace(txtFullName.Text),
            "Họ tên không được để trống!");

        ok &= Check(mtxtPhone, !mtxtPhone.MaskCompleted,
            "Vui lòng nhập đủ số điện thoại (000) 000-0000!");

        bool noCourse = cboCourse.SelectedValue is not int ma || ma <= 0;
        ok &= Check(cboCourse, noCourse, "Vui lòng chọn khóa học!");

        return ok;
    }

    // ----- Nút Đăng ký -----
    private async void btnRegister_Click(object? sender, EventArgs e)
    {
        if (!ValidateInput())
        {
            // Đưa con trỏ về ô lỗi đầu tiên
            foreach (Control c in new Control[] { txtFullName, mtxtPhone, cboCourse })
            {
                if (errorProvider.GetError(c).Length > 0)
                {
                    c.Focus();
                    break;
                }
            }
            return;
        }

        // Lấy thông tin khóa học đã chọn
        DataRow courseRow = ((DataRowView)cboCourse.SelectedItem!).Row;
        int maKH = (int)cboCourse.SelectedValue!;
        string tenKH = courseRow.Field<string>("TenKH")!;
        decimal hocPhi = courseRow.Field<decimal>("HocPhi");

        string gioiTinh = rdoMale.Checked ? "Nam" : "Nữ";

        // Gom các CheckBox đã tick
        var options = new List<string>();
        if (chkEnglish.Checked) options.Add(chkEnglish.Text);
        if (chkSoftSkill.Checked) options.Add(chkSoftSkill.Text);
        if (chkInternship.Checked) options.Add(chkInternship.Text);
        string? tuyChon = options.Count > 0 ? string.Join(", ", options) : null;

        var dk = new DangKy(
            txtFullName.Text.Trim(),
            gioiTinh,
            dtpBirthDate.Value.Date,
            mtxtPhone.Text.Trim(),
            maKH,
            tuyChon);

        btnRegister.Enabled = false;
        try
        {
            int id = await DangKyService.AddAsync(dk);

            // In thông tin đăng ký lên MessageBox
            var sb = new StringBuilder();
            sb.AppendLine("ĐĂNG KÝ THÀNH CÔNG!");
            sb.AppendLine();
            sb.AppendLine($"Mã đăng ký : {id}");
            sb.AppendLine($"Họ tên     : {dk.HoTen}");
            sb.AppendLine($"Giới tính  : {dk.GioiTinh}");
            sb.AppendLine($"Ngày sinh  : {dk.NgaySinh:dd/MM/yyyy}");
            sb.AppendLine($"Điện thoại : {dk.SoDienThoai}");
            sb.AppendLine($"Khóa học   : {tenKH} (mã {maKH})");
            sb.AppendLine($"Học phí    : {hocPhi.ToString("N0", ViCulture)} đ");
            sb.AppendLine($"Đăng ký thêm: {(tuyChon ?? "Không")}");

            MessageBox.Show(sb.ToString(), "Thông tin đăng ký",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            ResetForm();
            await LoadRegistrationsAsync();
        }
        catch (SqlException ex)
        {
            MessageBox.Show($"Không lưu được đăng ký:\n{ex.Message}", "Lỗi CSDL",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnRegister.Enabled = true;
        }
    }

    private void ResetForm()
    {
        txtFullName.Clear();
        rdoMale.Checked = true;
        dtpBirthDate.Value = new DateTime(2005, 1, 1);
        mtxtPhone.Clear();
        if (cboCourse.Items.Count > 0) cboCourse.SelectedIndex = 0;
        chkEnglish.Checked = false;
        chkSoftSkill.Checked = false;
        chkInternship.Checked = false;
        errorProvider.Clear();
        txtFullName.Focus();
    }
}

// =====================================================
//  Điểm khởi chạy
// =====================================================
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new frmDangKy());
    }
}
