import { ref, Ref } from "vue";

export function useRowHighlight() {
  const lastHighlightedRow: Ref<HTMLElement | null> = ref(null); // Biến để lưu trữ hàng đang được highlight

  const handleMouseOver = (event: MouseEvent) => {
    const td = event.target as HTMLElement;

    // Chỉ xử lý nếu phần tử đang hover là <td> (ô của bảng)
    if (td.tagName !== "TD") {
      return; // Bỏ qua nếu không phải là <td>
    }

    const row = td.parentNode as HTMLElement | null; // Lấy <tr> chứa <td>

    if (row && row !== lastHighlightedRow.value) {
      // Reset màu nền cho hàng trước đó (nếu có)
      if (lastHighlightedRow.value) {
        lastHighlightedRow.value.querySelectorAll("td").forEach((cell) => {
          cell.style.backgroundColor = "";
          cell.style.color = "";
          cell.style.fontWeight = "";
        });
      }

      // Highlight hàng mới
      // row.querySelectorAll("td").forEach((cell) => {
      //   cell.style.backgroundColor = "#ebeb28";
      //   cell.style.color = "black";
      //   // cell.style.fontWeight = "500";
      // });

      row.querySelectorAll("td").forEach((cell) => {
        cell.style.setProperty("background-color", "#ebeb28", "important");
        cell.style.setProperty("color", "black", "important");
        // cell.style.setProperty("font-weight", "500", "important");
      });

      // Cập nhật hàng hiện tại đang được highlight
      lastHighlightedRow.value = row;
    }
  };

  // Reset màu đỏ khi chuột rời khỏi toàn bộ bảng
  const handleMouseLeave = () => {
    if (lastHighlightedRow.value) {
      lastHighlightedRow.value.querySelectorAll("td").forEach((cell) => {
        cell.style.backgroundColor = ""; // Reset màu nền của hàng cuối cùng
        cell.style.color = "";
        cell.style.fontWeight = "";
      });
      lastHighlightedRow.value = null; // Reset lại biến hàng highlight cuối cùng
    }
  };

  return {
    handleMouseOver,
    handleMouseLeave
  };
}
