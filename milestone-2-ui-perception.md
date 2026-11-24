# Milestone 2 — Cải tiến UI, Perception & Hiệu năng

**Last Light** · Phụ trách: Ngan · 24/11/2026

Người chơi giờ biết guard đang để ý mình mà không bị lộ vị trí guard xuyên tường; guard nghe và phản ứng thực tế hơn; bộ nhớ rác do code game tạo ra mỗi frame giảm 72%.

## Vấn đề trước khi cải tiến

- Thanh mức độ nghi ngờ của guard hiện xuyên tường trong 60 m, như một "wallhack".
- Hình nón tầm nhìn của guard luôn hiện trong lúc chơi.
- Người chơi không biết mình sắp bị phát hiện, cũng không biết từ hướng nào.
- Guard nghe xuyên tường như ngoài trời trống.
- Nghe một tiếng động là guard biết chính xác vị trí người chơi.
- Guard bị bắn trúng chỉ nhấp nháy, không phản ứng.

## Cải tiến UI

| Phần | Trước | Sau |
| --- | --- | --- |
| Thông tin trên đầu guard | Thanh nghi ngờ và vai trò của mọi guard, kể cả sau tường | Chỉ hiện **?** (đang nghi ngờ) hoặc **!** (đã phát hiện) trên guard người chơi thực sự nhìn thấy |
| Thông tin debug | Luôn hiện | Gom vào phím F7, mặc định tắt: hình nón tầm nhìn, thanh nghi ngờ, vị trí cuối guard biết |
| Cảnh báo bị phát hiện | Không có | Vạch quanh tâm ngắm chỉ về phía guard: vàng và dài dần khi guard nghi ngờ, đỏ khi đã phát hiện |
| Hướng dẫn trên màn hình | Thiếu phím debug, có dòng chữ lỗi thời | Màn briefing thêm F3 / F7; chữ ở chế độ nhìn từ trên cập nhật đúng |

## Cải tiến Perception

| Cơ chế | Trước | Sau | Ví dụ |
| --- | --- | --- | --- |
| Tường cản âm thanh | Chỉ tính khoảng cách | Mỗi lớp tường giữa nguồn âm và guard làm khoảng nghe giảm một nửa (tính tối đa 3 lớp) | Tiếng súng 40 m → qua 1 tường còn 20 m, qua 2 tường còn 10 m |
| Vị trí nghe được | Chính xác tuyệt đối | Chỉ gần đúng: sai số 12% khoảng cách, thêm 50% mỗi lớp tường, tối đa 3 m; điểm đoán luôn là chỗ guard đi tới được | Nghe từ 10 m → lệch tới 1,2 m; từ 25 m → lệch tới 3 m |
| Phản ứng khi trúng đạn | Chỉ nghe tiếng súng (mức nghi ngờ) | Chuyển ngay sang đã phát hiện, biết vị trí người bắn, báo cho cả squad | Bắn lén một guard → cả nhóm biết hướng bạn |

Hệ quả: guard phải đi tìm thật sự khi chỉ nghe thấy, nên hành vi điều tra và tìm kiếm có ý nghĩa hơn; tường và đường đi vòng trở thành lựa chọn lén lút thật sự.

## Tối ưu hiệu năng

Bộ nhớ rác (GC) do code game tạo ra mỗi frame giảm từ 7,1 KB xuống 2,0 KB (−72%), gameplay giữ nguyên.

| Chỉ số (mỗi frame) | Trước | Sau | Thay đổi |
| --- | --- | --- | --- |
| Bộ nhớ rác của code game | 7,1 KB | 2,0 KB | −72% |
| Chi phí khởi tạo IMGUI | 2,9 KB | 0 | −100% |
| Bộ nhớ rác của perception (3 guard) | 0,5 KB | 8 byte | −98% |
| Thời gian vẽ giao diện (IMGUI) | 0,21 ms | 0,17 ms | −16% |
| FPS | 120 | 120 | Đã chạm trần 120 FPS |

Đã làm:
- Bỏ bước tính layout thừa của IMGUI (HUD chỉ dùng vị trí cố định).
- Gom phần vẽ trên đầu 3 guard vào một lần vẽ chung với HUD, thay vì mỗi guard tự vẽ.
- HUD lúc chơi chỉ vẽ ở bước repaint, bỏ qua các sự kiện chuột và bàn phím.
- Chuỗi chữ (đồng hồ, máu, đạn, mục tiêu) chỉ tạo lại khi giá trị thay đổi.
- Kiểm tra tầm nhìn của guard dùng lại bộ đệm thay vì tạo mảng mới mỗi frame.

Cách đo: một công cụ benchmark tự chạy map Substation 20 giây (sau 3 giây khởi động) trong cùng điều kiện, đọc số liệu từ Unity Profiler, chỉ tính phần code game. Máy Apple M5, Unity 6.6.

Ý nghĩa: ít bộ nhớ rác thì garbage collector chạy thưa hơn, giảm các lần giật khung hình ngắt quãng. FPS trung bình không đổi vì game vốn đã chạy ở mức trần 120 FPS. Khoảng 2 KB còn lại nằm trong hàm vẽ chữ của IMGUI; muốn về gần 0 cần chuyển HUD sang Canvas.

## Kết quả kiểm thử

- 20/20 bài kiểm thử gameplay tự động đạt trên cả hai map Substation và Rail Depot, gồm phát hiện bằng tầm nhìn, chia sẻ thông tin trong squad, che chắn, trí nhớ và thính giác. Chạy lại sau khi tối ưu hiệu năng: vẫn 20/20.
- Chơi thử trực tiếp: sau tiếng súng, guard hiện **?**; khi phát hiện người chơi thì chuyển **!** đỏ và vạch cảnh báo chỉ đúng hướng.

## Hướng phát triển tiếp

- Chỉ báo hướng trúng đạn thay cho viền đỏ cả màn hình
- Marker mục tiêu ghim ở mép màn hình khi nằm ngoài tầm nhìn
- Ngồi xuống (crouch) và tiếng bước chân khi đi bộ
- Guard dự đoán hướng người chơi di chuyển khi mất dấu
- Squad chia sẻ cả những lần "nghi ngờ" từ tiếng động
- Chuyển HUD sang Canvas để loại bỏ phần bộ nhớ rác còn lại
- Tăng tốc tìm đường A* bằng hàng đợi ưu tiên (priority queue)
