# Milestone 2 — Cải tiến UI & Perception

**Last Light** · Phụ trách: Ngan · 24/11/2026

Người chơi giờ biết guard đang để ý mình mà không bị lộ vị trí guard xuyên tường; guard nghe và phản ứng thực tế hơn.

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

## Kết quả kiểm thử

- 20/20 bài kiểm thử gameplay tự động đạt trên cả hai map Substation và Rail Depot, gồm phát hiện bằng tầm nhìn, chia sẻ thông tin trong squad, che chắn, trí nhớ và thính giác.
- Chơi thử trực tiếp: sau tiếng súng, guard hiện **?**; khi phát hiện người chơi thì chuyển **!** đỏ và vạch cảnh báo chỉ đúng hướng.

## Hướng phát triển tiếp

- Chỉ báo hướng trúng đạn thay cho viền đỏ cả màn hình
- Marker mục tiêu ghim ở mép màn hình khi nằm ngoài tầm nhìn
- Ngồi xuống (crouch) và tiếng bước chân khi đi bộ
- Guard dự đoán hướng người chơi di chuyển khi mất dấu
- Squad chia sẻ cả những lần "nghi ngờ" từ tiếng động
