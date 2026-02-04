#include <iostream>
#include <thread>
#include <chrono>

// Forward declarations
void module_a();
void module_b();
void module_c();
void module_d();

int main() {
    std::cout << "Parallel compile test - all modules loaded!" << std::endl;
    module_a();
    module_b();
    module_c();
    module_d();
    return 0;
}
