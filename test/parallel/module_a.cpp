#include <iostream>
#include <vector>
#include <algorithm>
#include <numeric>

void module_a() {
    std::vector<int> data(1000);
    std::iota(data.begin(), data.end(), 0);
    auto sum = std::accumulate(data.begin(), data.end(), 0);
    std::cout << "Module A: sum = " << sum << std::endl;
}
