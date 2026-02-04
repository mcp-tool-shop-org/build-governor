#include <iostream>
#include <memory>
#include <array>

void module_c() {
    auto ptr = std::make_unique<std::array<double, 100>>();
    (*ptr)[0] = 3.14159;
    std::cout << "Module C: pi = " << (*ptr)[0] << std::endl;
}
