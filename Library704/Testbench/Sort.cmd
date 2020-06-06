iverilog -o Sort.vvp I704.v system_tb_sort.v
vvp Sort.vvp
gtkwave Sort.vcd -A