# Kế hoạch bản trình diễn chiến lược của đội lính

Bản trình diễn dùng bản đồ Substation (trạm điện). Người xem có thể đứng ngoài quan sát hoặc tự điều khiển người đột nhập. Mục tiêu là nhìn một lượt cũng hiểu lính vừa biết thêm điều gì, vì sao cả đội đổi cách phối hợp, và từng lính sẽ đi đâu.

## Đội lính biết gì?

- **Từ đồng đội:** Ai vừa thấy người chơi, ai nghe thấy tiếng động, ai còn đạn để yểm trợ (canh hướng nguy hiểm cho người khác di chuyển), ai đang nạp đạn hoặc bị thương.
- **Từ người chơi:** Chỗ nhìn thấy gần nhất, chỗ phát ra tiếng động và sát thương vừa nhận. Mỗi tin có người báo và thời điểm. Khi không còn ai thấy người chơi, đội chỉ dùng tin đã ghi lại. Tin cũ sẽ hết hạn.
- **Từ bản đồ:** Tường có chắn tầm nhìn không, chỗ nào có thể trú, và đường nào thật sự đi được. Nếu đi hụt vì có vật chắn, đội thử tìm lối khác rồi đổi kế hoạch nếu cần.

## Đội sẽ làm gì?

| Chiến lược | Khi nào dùng | Đội sẽ làm gì |
| --- | --- | --- |
| Trinh sát theo lượt | Chưa có tin đáng tin | Một lính đi kiểm tra khu vực, một lính đi theo để hỗ trợ, một lính đứng quan sát. Khi đến điểm kiểm tra, đổi người dẫn đường. |
| Giữ hướng chính, vòng sang bên | Có lính nhìn thấy người chơi, còn đạn và có đường vòng | Một lính giữ hướng đang thấy người chơi. Một lính đi đường vòng; nếu người yểm trợ phải nạp đạn thì người đi vòng dừng lại. |
| Chia hướng tìm | Người chơi vừa khuất tầm nhìn hoặc đội vừa nghe tiếng động | Hai lính kiểm tra hai điểm khác nhau gần chỗ có tin. Một lính ở lại canh hướng cũ. Nếu bắt gặp lại người chơi, đội dùng tin mới ngay. |
| Tiến từng lượt | Chỉ có một lối tốt để kiểm tra chỗ có tin | Một lính tiến một đoạn theo đường đi được; đồng đội đứng canh. Đến nơi hoặc phải tìm lối khác thì đổi lượt. |
| Đổi góc bắn | Đang thấy người chơi nhưng tường chắn góc bắn hoặc thiếu đường vòng | Một lính quan sát hoặc giữ góc đang bắn được. Người khác đi tìm góc nhìn mới. Nếu có chỗ trú gần đó, lính có thể lui vào, nạp đạn rồi bước ra bắn tiếp. |
| Đưa người bị thương về chỗ trú | Có lính dưới 35 phần trăm máu và có đường rút | Lính bị thương chạy tới chỗ khuất. Người còn đạn giữ hướng nguy hiểm. Khi mất đường rút, đội đánh giá lại thay vì tiếp tục chạy vào tường. |

## Người xem sẽ thấy gì?

- Một đường có vệt sáng chạy dọc theo **đường lính thật sự đi**, uốn qua các góc tường. Đường dự kiến khi lính đang chờ có nét đứt. Vòng tròn ở đích nở ra khi có lệnh mới hoặc khi lính tới nơi.
- Tiếng động hoặc tin mới từ đồng đội tạo vòng sóng ở điểm được báo. Màu đổi khi đội nhìn thấy lại người chơi, và dấu cũ mờ dần.
- Đường nối giữa hai lính cho biết ai đang yểm trợ ai. Vệt chạy trên đường nối chỉ hiện khi người yểm trợ sẵn sàng.
- Bảng trên cùng hiện tên kế hoạch, lý do chọn và hiệu ứng nháy khi đổi. Nhật ký bên trái ghi người báo tin, đổi vai và đường bị chặn.

## Các giả định mình chọn

- Bản trình diễn tách khỏi hai màn chơi chính. Có một mục trong trình soạn thảo Unity để tạo và mở màn trình diễn. Ưu tiên đổi cách phối hợp rõ ràng, dễ quan sát.
- Đội có ba lính. Đội xem xét lại khoảng bốn lần mỗi giây. Một kế hoạch còn phù hợp sẽ được giữ khoảng một giây để tránh đổi qua đổi lại. Khi kế hoạch mất điều kiện hoặc có người cần rút lui gấp, đội có thể đổi ở lần xem xét kế tiếp. Tin cuối cùng có giá trị tối đa tám giây.
- Dùng đường tìm sẵn trong trò chơi và các chỗ trú đã đặt trên bản đồ. Cách phối hợp lấy cảm hứng từ việc đọc hướng và phối hợp trong các trò chơi bắn súng góc nhìn thứ nhất. Màn chơi và hình ảnh vẫn là của Last Light.
- Có các tình huống dựng sẵn để lặp lại khi thuyết trình. Bản trình diễn bắt đầu ở góc nhìn toàn cảnh và cho bạn điều khiển nhân vật ngay. Phím `V` chuyển giữa tự chơi và đứng xem. Khi tự chơi, lính có thể hạ bạn. Khi đứng xem, nhân vật được bảo vệ để buổi trình diễn không bị cắt ngang.

## Ba mức độ

Lần đầu mở, mức mặc định là **Trung bình**. Bấm `M` để đổi ngay trong bản trình diễn. Mức đã chọn được giữ khi chơi lại.

- **Dễ:** Lính đổi chỗ chậm và chờ lâu hơn trước khi bắn. Người chơi có nhiều thời gian để chạy đến chỗ trú.
- **Trung bình:** Đội tìm góc mới khi bị tường chắn, đổi người đi đầu và luân phiên bắn với nạp đạn sau chỗ trú.
- **Khó:** Đội xem lại kế hoạch thường xuyên hơn, cho nhiều lính đi tìm khi mất dấu, ít chờ trước khi di chuyển và bắn nhanh hơn. Khi mất dấu, đội dùng chỗ được báo gần nhất để tìm, rồi quay lại trinh sát khi tin hết hạn.
- Thân nhân vật có chân chuyển động khi di chuyển. Thân hiện từ góc nhìn toàn cảnh và góc theo lính. Khi chuyển sang góc nhìn của nhân vật, màn hình vẫn chỉ hiện tay và súng như trước.

## Cách kiểm tra

1. Mở **Tactical Game → Utility AI → Open squad tactics demo** (mở màn trình diễn chiến lược, trong đó AI là Artificial Intelligence, nghĩa là trí tuệ nhân tạo). Bấm **Play** (chạy thử), rồi **Begin Mission** (bắt đầu).
2. Dùng `0` để xem toàn cảnh hoặc `4` để chuyển về góc nhìn của nhân vật. `Tab` đổi nhanh giữa hai góc nhìn. Khi xem từ trên cao, cuộn chuột hoặc giữ `U` để phóng to và `J` để thu nhỏ. Dùng `W`, `A`, `S`, `D` để di chuyển theo hướng trên màn hình và dùng chuột để ngắm.
3. Dùng `X` để nhân vật tự đi lộ diện, `H` để tự đi nấp và `B` để làm một lính bị thương. Các tình huống này chuyển sang chế độ đứng xem. Bấm `V` để lấy lại điều khiển. `N` phát tiếng động. `O` đặt hoặc gỡ vật cản khi lính đang đi. `1` đến `3` theo từng lính, `P` tạm dừng và `L` chạy chậm.
4. Kiểm tra tên kế hoạch, lý do đổi, đường lính đi quanh tường, người yểm trợ ngừng báo sẵn sàng khi nạp đạn và dấu vị trí mờ đi khi tin hết hạn.

## Đã kiểm tra

Trong Unity 6000.6, đội đã đổi kế hoạch khi người chơi lộ diện, đi nấp, phát tiếng động và làm lính bị thương. Đặt vật cản giữa lúc lính đang đi làm đội tính lại đường. Đội bỏ tin khi hết hạn và nhận tin mới khi có tiếng động. Bản chạy trên macOS cũng cho thấy vệt sáng đi qua các góc đường và vòng sóng nở ra trên bản đồ.
