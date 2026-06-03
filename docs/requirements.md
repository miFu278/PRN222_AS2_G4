# Đặc Tả Yêu Cầu Dự Án: RAG ChatBot Học Tập (System Requirements Specification - SRS)

Dự án **RAG ChatBot** là một ứng dụng web hỗ trợ học tập, cho phép Giảng viên tải lên các tài liệu môn học (PDF, TXT) để AI tự động phân tích và tạo cơ sở tri thức. Sinh viên có thể trò chuyện với Chatbot AI để hỏi đáp, giải thích bài tập và nhận câu trả lời chính xác được trích xuất từ chính nguồn tài liệu do Giảng viên cung cấp.

---

## 1. Đối Tượng Người Dùng (Actors)

Hệ thống phân chia thành 3 vai trò (Roles) chính với các quyền hạn khác nhau:
- **Sinh viên (Student)**:
  - Xem danh sách môn học, xem các tài liệu tham khảo trực tuyến.
  - Chat với AI Chatbot trong phạm vi kiến thức của môn học để đặt câu hỏi và nhận câu trả lời kèm trích nguồn tài liệu.
- **Giảng viên (Lecturer)**:
  - Có toàn bộ quyền hạn của Sinh viên.
  - Tạo và quản lý các Môn học do mình phụ trách.
  - Tải lên tài liệu (.pdf, .txt) và quản lý tài liệu (Xóa, xem trạng thái xử lý).
  - Tùy chọn nâng cấp gói tài khoản lên Premium để tăng hạn mức tải tài liệu.
- **Quản trị viên (Admin)**:
  - Có toàn quyền kiểm soát hệ thống.
  - Quản lý danh sách người dùng, phân quyền vai trò.
  - Cấu hình và theo dõi hoạt động của hệ thống AI.

---

## 2. Yêu Cầu Chức Năng (Functional Requirements)

### 2.1. Quản lý Tài khoản & Xác thực (Auth & Authorization)
- **Đăng nhập/Đăng ký**: Hỗ trợ đăng nhập bằng tài khoản cục bộ (Username/Password sử dụng mã hóa BCrypt).
- **Google SSO (OAuth 2.0)**: Cho phép người dùng đăng nhập nhanh bằng tài khoản Google. Tự động tạo tài khoản mới nếu email chưa tồn tại trong hệ thống (Auto-provisioning với Role mặc định là Student và Tier là Free).
- **Phân quyền người dùng**: Sử dụng Cookie-based Authentication với các quyền dựa trên vai trò (Lecturer/Admin được phép upload/xóa tài liệu, Student chỉ được xem và chat).

### 2.2. Quản lý Gói cước (Premium Subscription)
- **Xem Bảng giá (Pricing)**: Người dùng có thể xem thông tin các gói dịch vụ (Free và Premium).
- **Thanh toán mô phỏng (Mock Payment)**: Thực hiện nâng cấp tài khoản thông qua cổng thanh toán mô phỏng.
- **Tự động cập nhật quyền hạn**: Sau khi thanh toán thành công, hệ thống tự động cập nhật `SubscriptionTier = Premium` trong Database và làm mới Cookie claims ngầm (người dùng trải nghiệm tính năng Premium ngay lập tức mà không cần đăng xuất).

### 2.3. Quản lý Tri thức & Xử lý Tài liệu (Knowledge Management - Indexing Pipeline)
- **Tạo môn học**: Giảng viên tạo môn học kèm mã code môn học (ví dụ: CS101) và chương/chủ đề học tập.
- **Upload Tài liệu**: Giảng viên tải lên tài liệu tham khảo (.pdf, .txt, .md).
  - *Hạn mức gói cước*: Gói Free giới hạn file tối đa **5 MB**. Gói Premium mở rộng hạn mức tối đa lên **50 MB**.
- **Lưu trữ Cloud**: Tệp tin thô được lưu trữ an toàn trên **Supabase Cloud Storage**.
- **Xử lý tài liệu ngầm (Background Worker)**:
  - Tự động phát hiện tài liệu mới tải lên qua Background Worker chạy ngầm để không gây nghẽn luồng HTTP request.
  - **Trích xuất văn bản (Text Extraction)**: Đọc và trích xuất chữ thô từ tệp PDF bằng thư viện `PdfPig`.
  - **Phân đoạn văn bản (Chunking)**: Chia nhỏ văn bản dài thành các đoạn (chunks) 500 ký tự, có độ gối đầu (overlap 50 ký tự) để giữ ngữ cảnh liền mạch.
  - **Vector hóa (Embedding)**: Gửi các chunk văn bản tới API Embedding (chuẩn OpenAI thông qua 9router hoặc GitHub Models) để nhận về Vector 1536 chiều đại diện cho ngữ nghĩa.
  - **Lưu trữ Vector DB**: Lưu văn bản thô cùng mảng Vector tương ứng vào bảng `DocumentChunks` của PostgreSQL sử dụng extension **pgvector**.
- **Theo dõi trạng thái real-time**: Giao diện hiển thị trạng thái xử lý tài liệu ("Chờ xử lý" / "Đã xử lý") và tự động cập nhật động (AJAX Polling 3 giây/lần) khi Background Worker hoàn thành.

### 2.4. Hỏi đáp Chatbot AI (RAG Chat - Retrieval & Generation Pipeline)
- **Lịch sử hội thoại**: Tự động lưu và hiển thị lại lịch sử các câu hỏi và trả lời của mỗi phiên chat.
- **Tìm kiếm ngữ cảnh (Retrieval)**: Khi học sinh gửi câu hỏi -> Hệ thống Vector hóa câu hỏi -> Thực hiện truy vấn độ tương đồng (Similarity Search) lọc theo môn học để tìm ra Top-K đoạn tài liệu chứa nội dung liên quan nhất trong DB.
- **Sinh câu trả lời (Generation)**: Gộp ngữ cảnh tìm được cùng câu hỏi và lịch sử chat gần đây gửi tới LLM (ví dụ: GPT-4o hoặc Gemini qua 9router/GitHub Models) để sinh câu trả lời tự nhiên.
- **Trích nguồn tài liệu (Citations)**: Câu trả lời hiển thị cho học sinh phải ghi rõ nguồn trích dẫn lấy từ tài liệu nào, chương nào của giảng viên.

---

## 3. Yêu Cầu Phi Chức Năng (Non-Functional Requirements)

### 3.1. Hiệu năng & Trải nghiệm (Performance & UX)
- Phân tách tiến trình xử lý nặng (AI vector hóa) chạy ngầm dưới nền, phản hồi giao diện tải lên thành công tức thì cho người dùng chỉ dưới 1 giây.
- Giao diện tối ưu hóa cho màn hình Desktop và Mobile (Responsive Layout). Sử dụng hiệu ứng mượt mà và thông báo trực quan.

### 3.2. Bảo mật (Security)
- Mật khẩu tài khoản cục bộ được mã hóa bằng chuẩn **BCrypt.Net-Next**.
- Chỉ cho phép Giảng viên sở hữu môn học hoặc tài khoản Admin được quyền xóa tài liệu của môn học đó.
- Không cho phép can thiệp chéo hay truy cập trái phép API của các môn học khác.

### 3.3. Khả năng mở rộng & Bảo trì (Extensibility)
- Thiết kế theo cấu trúc **Clean Architecture** (Domain -> Application -> Infrastructure -> Presentation) giúp phân tách rõ ràng nghiệp vụ cốt lõi và tích hợp hạ tầng.
- Dễ dàng thay thế cổng API AI (như 9router, OpenAI, Gemini trực tiếp) hoặc thay thế cơ sở dữ liệu Vector DB mà chỉ cần thay đổi cấu hình hoặc sửa đổi lớp Implementation ở tầng Infrastructure, không làm ảnh hưởng tới tầng Logic/Domain.

---

## 4. Công Nghệ Sử Dụng (Technology Stack)

- **Backend**: C# ASP.NET Core Web App (.NET 9.0)
- **Database chính & Vector DB**: PostgreSQL (lưu trên **Supabase Cloud**) tích hợp extension **pgvector** và thư viện `Pgvector.EntityFrameworkCore`.
- **Lưu trữ file vật lý**: **Supabase Storage** (Bucket: `raw-documents`).
- **Trích xuất văn bản**: Thư viện mã nguồn mở `UglyToad.PdfPig` (xử lý tệp PDF).
- **AI Gateway (Embeddings & LLM)**: **9router** hoặc **GitHub Inference API** (Hỗ trợ chuẩn kết nối tương thích OpenAI, model `text-embedding-3-small` cho vector nhúng).
- **Frontend**: Razor Pages, HTML/CSS (Vanilla UI), JS (AJAX Polling real-time).
