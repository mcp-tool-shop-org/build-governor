#include <iostream>
#include <functional>
#include <optional>

void module_d() {
    std::optional<int> value = 42;
    std::function<int(int)> fn = [](int x) { return x * 2; };
    if (value) {
        std::cout << "Module D: result = " << fn(*value) << std::endl;
    }
}
